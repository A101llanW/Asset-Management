# Converts active Class departments (under Grade) to Room under Academics (or chosen top-level admin).
param(
    [string]$OrganizationSlug,
    [int]$TopLevelAdministrativeDepartmentId = 0,
    [switch]$AllOrganizations,
    [switch]$Apply,
    [string]$ServerInstance,
    [string]$Database,
    # Folder for the per-org rollback .sql written on -Apply (default: tools\database\rollback).
    [string]$RollbackFolder
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\_Common.ps1"

$KindAdmin = 0
$KindGrade = 1
$KindClass = 2
$KindSub = 3
$KindRoom = 4
$MaxCodeLen = 20
$ContainerSuffix = ' homerooms'

function Normalize-Token([string]$text, [int]$maxLen) {
    if ([string]::IsNullOrWhiteSpace($text)) { return '' }
    $t = ($text -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
    if ($t.Length -gt $maxLen) { $t = $t.Substring(0, $maxLen) }
    return $t
}

function Get-UniqueCode([string]$baseCode, $existingCodes) {
    $code = $baseCode
    if ([string]::IsNullOrWhiteSpace($code)) { $code = 'HR' }
    if ($code.Length -gt $MaxCodeLen) { $code = $code.Substring(0, $MaxCodeLen) }
    if (-not $existingCodes.Contains($code)) { return $code }
    for ($i = 2; $i -lt 100; $i++) {
        $candidate = $code + $i
        if ($candidate.Length -gt $MaxCodeLen) {
            $candidate = $candidate.Substring(0, $MaxCodeLen)
        }
        if (-not $existingCodes.Contains($candidate)) { return $candidate }
    }
    throw "Could not allocate unique department code for '$baseCode'."
}

function Convert-OrganizationClasses {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [int]$OrganizationId,
        [string]$Slug,
        [int]$AdminId,
        [bool]$DoApply
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = @"
SELECT [Id],[Name],[Code],[DepartmentKind],[ParentDepartmentId],[IsRequisitionTarget],
       [ClassTeacherUserId],[RoomCustodianUserId],[IsActive]
FROM [Department]
WHERE [OrganizationId] = @orgId AND [IsActive] = 1
"@
    $cmd.Parameters.AddWithValue('@orgId', $OrganizationId) | Out-Null
    $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $table = New-Object System.Data.DataTable
    [void]$da.Fill($table)

    $rows = @{}
    $codes = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $table.Rows) {
        $rows[$r.Id] = $r
        if ($r.Code) { [void]$codes.Add([string]$r.Code) }
    }

    if (-not $rows.ContainsKey($AdminId)) {
        throw "Admin department #$AdminId not found in organization '$Slug'."
    }
    $adminRow = $rows[$AdminId]
    $adminKind = [int]$adminRow.DepartmentKind
    $adminParent = $adminRow.ParentDepartmentId
    $adminHasParent = ($null -ne $adminParent -and $adminParent -ne [DBNull]::Value -and [int]$adminParent -gt 0)
    if ($adminKind -ne $KindAdmin -or $adminHasParent) {
        throw "Department #$AdminId must be a top-level administrative unit in '$Slug' (kind=$adminKind, parent=$adminParent)."
    }

    $classes = $table.Rows | Where-Object { [int]$_.DepartmentKind -eq $KindClass }
    if (-not $classes -or $classes.Count -eq 0) {
        Write-Host "  [$Slug] No active classes - skip." -ForegroundColor DarkGray
        return
    }

    Write-Host "  [$Slug] $($classes.Count) class(es); host admin: $($adminRow.Name) (#$AdminId)" -ForegroundColor Cyan

    $containerByGrade = @{}
    $inserts = New-Object System.Collections.Generic.List[object]
    $updates = New-Object System.Collections.Generic.List[object]

    foreach ($classRow in $classes) {
        $gradeId = $classRow.ParentDepartmentId
        if (-not $gradeId -or -not $rows.ContainsKey([int]$gradeId)) {
            Write-Warning "  Skip class '$($classRow.Name)': missing grade parent."
            continue
        }
        $grade = $rows[[int]$gradeId]
        if ([int]$grade.DepartmentKind -ne $KindGrade) {
            Write-Warning "  Skip class '$($classRow.Name)': parent is not a grade."
            continue
        }

        if (-not $containerByGrade.ContainsKey([int]$gradeId)) {
            $containerName = ($grade.Name.ToString().Trim() + $ContainerSuffix)
            $existing = $table.Rows | Where-Object {
                ([int]$_.DepartmentKind -eq $KindSub) -and ($_.ParentDepartmentId -eq $AdminId) -and ($_.Name.ToString() -eq $containerName)
            } | Select-Object -First 1
            if ($existing) {
                $containerByGrade[[int]$gradeId] = [int]$existing.Id
                Write-Host "    Reuse container '$containerName' (#$($existing.Id))" -ForegroundColor DarkGray
            }
            else {
                $parentCode = [string]$adminRow.Code
                $subToken = Normalize-Token ($grade.Name + '-HR') 10
                $parentToken = Normalize-Token $parentCode 8
                $newCode = Get-UniqueCode ($parentToken + '-' + $subToken) $codes
                [void]$codes.Add($newCode)
                $tempId = -1000 - $inserts.Count
                $containerByGrade[[int]$gradeId] = $tempId
                $inserts.Add([pscustomobject]@{
                    TempId = $tempId
                    GradeId = [int]$gradeId
                    Name = $containerName
                    Code = $newCode
                    Description = "Homeroom rooms converted from class streams under $($grade.Name)"
                })
                Write-Host "    Create container '$containerName' ($newCode)" -ForegroundColor Yellow
            }
        }

        $parentContainer = $containerByGrade[[int]$gradeId]
        $updates.Add([pscustomobject]@{
            ClassId = [int]$classRow.Id
            ClassName = [string]$classRow.Name
            ParentId = $parentContainer
            Custodian = $classRow.ClassTeacherUserId
            OldParentId = [int]$gradeId
            OldTeacher = $classRow.ClassTeacherUserId
            OldCustodian = $classRow.RoomCustodianUserId
        })
    }

    if (-not $DoApply) {
        Write-Host "    [Preview] $($inserts.Count) sub-unit(s), $($updates.Count) class-to-room update(s)." -ForegroundColor Green
        return
    }

    $tran = $Connection.BeginTransaction()
    try {
        $mapTemp = @{}
        foreach ($ins in $inserts) {
            $ic = $Connection.CreateCommand()
            $ic.Transaction = $tran
            $ic.CommandText = @"
INSERT INTO [Department] ([OrganizationId],[Name],[Code],[Description],[ParentDepartmentId],[DepartmentKind],
    [IsRequisitionTarget],[IsActive],[CreatedAt],[UpdatedAt],[RequisitionFlowMode])
VALUES (@orgId,@name,@code,@desc,@parentId,@kind,0,1,GETUTCDATE(),GETUTCDATE(),0);
SELECT CAST(SCOPE_IDENTITY() AS INT);
"@
            $ic.Parameters.AddWithValue('@orgId', $OrganizationId) | Out-Null
            $ic.Parameters.AddWithValue('@name', $ins.Name) | Out-Null
            $ic.Parameters.AddWithValue('@code', $ins.Code) | Out-Null
            $ic.Parameters.AddWithValue('@desc', $ins.Description) | Out-Null
            $ic.Parameters.AddWithValue('@parentId', $AdminId) | Out-Null
            $ic.Parameters.AddWithValue('@kind', $KindSub) | Out-Null
            $newId = [int]$ic.ExecuteScalar()
            $mapTemp[$ins.TempId] = $newId
        }

        if ([int]$adminRow.IsRequisitionTarget -eq 1) {
            $ac = $Connection.CreateCommand()
            $ac.Transaction = $tran
            $ac.CommandText = 'UPDATE [Department] SET [IsRequisitionTarget]=0,[UpdatedAt]=GETUTCDATE() WHERE [Id]=@id'
            $ac.Parameters.AddWithValue('@id', $AdminId) | Out-Null
            [void]$ac.ExecuteNonQuery()
        }

        foreach ($upd in $updates) {
            $parentId = $upd.ParentId
            if ($parentId -lt 0) {
                $parentId = $mapTemp[$parentId]
            }
            $uc = $Connection.CreateCommand()
            $uc.Transaction = $tran
            $uc.CommandText = @"
UPDATE [Department]
SET [DepartmentKind]=@roomKind,
    [ParentDepartmentId]=@parentId,
    [RoomCustodianUserId]=@custodian,
    [ClassTeacherUserId]=NULL,
    [UpdatedAt]=GETUTCDATE()
WHERE [Id]=@id AND [DepartmentKind]=@classKind
"@
            $uc.Parameters.AddWithValue('@roomKind', $KindRoom) | Out-Null
            $uc.Parameters.AddWithValue('@classKind', $KindClass) | Out-Null
            $uc.Parameters.AddWithValue('@parentId', $parentId) | Out-Null
            $cust = $upd.Custodian
            if ($cust) { $uc.Parameters.AddWithValue('@custodian', $cust) | Out-Null }
            else { $uc.Parameters.AddWithValue('@custodian', [DBNull]::Value) | Out-Null }
            $uc.Parameters.AddWithValue('@id', $upd.ClassId) | Out-Null
            $n = $uc.ExecuteNonQuery()
            if ($n -ne 1) {
                throw "Expected to convert class #$($upd.ClassId) ($($upd.ClassName)) but updated $n row(s)."
            }
        }

        $tran.Commit()
        Write-Host "    Applied: $($inserts.Count) container(s), $($updates.Count) room(s)." -ForegroundColor Green

        $q = { param($v) if ($null -eq $v -or $v -eq [DBNull]::Value -or [string]::IsNullOrWhiteSpace([string]$v)) { 'NULL' } else { "N'" + ([string]$v).Replace("'", "''") + "'" } }
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.AppendLine("-- Rollback for Convert-ClassesToRooms on org #$OrganizationId ($Slug), generated $(Get-Date -Format s)")
        [void]$sb.AppendLine('SET XACT_ABORT ON; BEGIN TRANSACTION;')
        foreach ($upd in $updates) {
            [void]$sb.AppendLine("UPDATE [Department] SET [DepartmentKind]=$KindClass,[ParentDepartmentId]=$($upd.OldParentId),[ClassTeacherUserId]=$(& $q $upd.OldTeacher),[RoomCustodianUserId]=$(& $q $upd.OldCustodian),[UpdatedAt]=GETUTCDATE() WHERE [Id]=$($upd.ClassId) AND [OrganizationId]=$OrganizationId AND [DepartmentKind]=$KindRoom;")
        }
        if ([int]$adminRow.IsRequisitionTarget -eq 1) {
            [void]$sb.AppendLine("UPDATE [Department] SET [IsRequisitionTarget]=1,[UpdatedAt]=GETUTCDATE() WHERE [Id]=$AdminId AND [OrganizationId]=$OrganizationId;")
        }
        foreach ($newId in $mapTemp.Values) {
            [void]$sb.AppendLine("IF EXISTS (SELECT 1 FROM [Department] WHERE [ParentDepartmentId]=$newId) THROW 50001, 'Container #$newId still has children; rollback aborted.', 1;")
            [void]$sb.AppendLine("DELETE FROM [Department] WHERE [Id]=$newId AND [OrganizationId]=$OrganizationId;")
        }
        [void]$sb.AppendLine('COMMIT TRANSACTION;')
        $folder = $RollbackFolder
        if (-not $folder) { $folder = Join-Path $PSScriptRoot 'rollback' }
        if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder | Out-Null }
        $rbPath = Join-Path $folder ("rollback-c2r-org$OrganizationId-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.sql')
        [IO.File]::WriteAllText($rbPath, $sb.ToString())
        Write-Host "    Rollback script: $rbPath" -ForegroundColor Green
    }
    catch {
        $tran.Rollback()
        throw
    }
}

$target = Resolve-SqlTargetFromWebConfig -ServerInstance $ServerInstance -Database $Database
$connStr = "Data Source=$($target.ServerInstance);Initial Catalog=$($target.Database);Integrated Security=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

if (-not $AllOrganizations -and [string]::IsNullOrWhiteSpace($OrganizationSlug)) {
    throw 'Specify -OrganizationSlug <slug> or -AllOrganizations.'
}

$orgCmd = $conn.CreateCommand()
if ($AllOrganizations) {
    $orgCmd.CommandText = @"
SELECT o.[Id], o.[Slug],
  (SELECT COUNT(*) FROM [Department] d WHERE d.[OrganizationId]=o.[Id] AND d.[IsActive]=1 AND d.[DepartmentKind]=2) AS ClassCount
FROM [Organization] o
WHERE EXISTS (
  SELECT 1 FROM [Department] d WHERE d.[OrganizationId]=o.[Id] AND d.[IsActive]=1 AND d.[DepartmentKind]=2
)
ORDER BY o.[Slug]
"@
}
else {
    $orgCmd.CommandText = @"
SELECT o.[Id], o.[Slug],
  (SELECT COUNT(*) FROM [Department] d WHERE d.[OrganizationId]=o.[Id] AND d.[IsActive]=1 AND d.[DepartmentKind]=2) AS ClassCount
FROM [Organization] o WHERE o.[Slug]=@slug
"@
    $orgCmd.Parameters.AddWithValue('@slug', $OrganizationSlug.Trim()) | Out-Null
}

$oda = New-Object System.Data.SqlClient.SqlDataAdapter $orgCmd
$orgs = New-Object System.Data.DataTable
[void]$oda.Fill($orgs)
if ($orgs.Rows.Count -eq 0) {
    Write-Host 'No active class departments remain in the selected scope.' -ForegroundColor Green
    $conn.Close()
    return
}

foreach ($org in $orgs.Rows) {
    $orgId = [int]$org.Id
    $slug = [string]$org.Slug
    $adminId = $TopLevelAdministrativeDepartmentId
    if ($adminId -le 0) {
        $findAdmin = $conn.CreateCommand()
        $findAdmin.CommandText = @"
SELECT TOP 1 [Id] FROM [Department]
WHERE [OrganizationId]=@orgId AND [IsActive]=1 AND [DepartmentKind]=@adminKind
  AND [ParentDepartmentId] IS NULL
  AND ([Name]='Academics' OR [Code]='ACADEMICS')
ORDER BY [Id]
"@
        $findAdmin.Parameters.AddWithValue('@orgId', $orgId) | Out-Null
        $findAdmin.Parameters.AddWithValue('@adminKind', $KindAdmin) | Out-Null
        $found = $findAdmin.ExecuteScalar()
        if (-not $found) {
            Write-Warning "[$slug] No Academics admin - skip (pass -TopLevelAdministrativeDepartmentId)."
            continue
        }
        $adminId = [int]$found
    }

    Convert-OrganizationClasses -Connection $conn -OrganizationId $orgId -Slug $slug -AdminId $adminId -DoApply:$Apply
}

$conn.Close()
if (-not $Apply) {
    Write-Host ''
    Write-Host 'Preview only. Re-run with -Apply to execute.' -ForegroundColor Yellow
}
