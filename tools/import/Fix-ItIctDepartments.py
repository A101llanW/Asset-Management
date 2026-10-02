#!/usr/bin/env python3
"""Ensure ICT is a sub-department under IT and retire ICT-ALL departments."""

from __future__ import annotations

import argparse

import pyodbc

CONNECTION = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=.\\SQLEXPRESS;"
    "DATABASE=AssetManagementModuleDb;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)


def connect():
    drivers = [driver for driver in pyodbc.drivers() if "SQL Server" in driver]
    if not drivers:
        raise RuntimeError("No SQL Server ODBC driver found.")
    return pyodbc.connect(CONNECTION.replace("ODBC Driver 17 for SQL Server", drivers[-1]), autocommit=False)


def fetch_one(cursor, query, *params):
    cursor.execute(query, params)
    row = cursor.fetchone()
    return row[0] if row else None


def ensure_sub_department(cursor, org_id: int, parent_code: str, sub_name: str, sub_code: str) -> int:
    parent_id = fetch_one(
        cursor,
        "SELECT Id FROM Department WHERE OrganizationId = ? AND Code = ? AND IsActive = 1",
        org_id,
        parent_code,
    )
    if parent_id is None:
        raise RuntimeError(f"Parent department {parent_code} not found for org {org_id}")

    sub_id = fetch_one(
        cursor,
        "SELECT Id FROM Department WHERE OrganizationId = ? AND Code = ? AND IsActive = 1",
        org_id,
        sub_code,
    )
    if sub_id is not None:
        return sub_id

    cursor.execute(
        """
        INSERT INTO Department (
            OrganizationId, Name, Code, Description, ParentDepartmentId,
            DepartmentKind, IsRequisitionTarget, CreatedAt, IsActive
        )
        VALUES (?, ?, ?, ?, ?, 1, 1, GETUTCDATE(), 1)
        """,
        org_id,
        sub_name,
        sub_code,
        f"{sub_name} ({parent_code})",
        parent_id,
    )
    cursor.execute("SELECT @@IDENTITY")
    sub_id = int(cursor.fetchone()[0])

    cursor.execute(
        """
        UPDATE Department
        SET IsRequisitionTarget = 0, UpdatedAt = GETUTCDATE()
        WHERE Id = ? AND IsRequisitionTarget = 1
        """,
        parent_id,
    )
    return sub_id


def fix_org(cursor, org_id: int, dry_run: bool) -> None:
    ict_sub_id = ensure_sub_department(cursor, org_id, "IT", "ICT", "IT-ICT")

    ict_all_id = fetch_one(
        cursor,
        "SELECT Id FROM Department WHERE OrganizationId = ? AND Code = 'ICT-ALL'",
        org_id,
    )
    if ict_all_id is not None:
        cursor.execute(
            "UPDATE Asset SET DepartmentId = ?, UpdatedAt = GETUTCDATE() WHERE OrganizationId = ? AND DepartmentId = ?",
            ict_sub_id,
            org_id,
            ict_all_id,
        )
        moved = cursor.rowcount
        print(f"  Moved {moved} assets from ICT-ALL to IT-ICT")

    standalone_ict_id = fetch_one(
        cursor,
        "SELECT Id FROM Department WHERE OrganizationId = ? AND Code = 'ICT' AND ParentDepartmentId IS NULL",
        org_id,
    )
    if standalone_ict_id is not None:
        cursor.execute(
            "UPDATE Asset SET DepartmentId = ?, UpdatedAt = GETUTCDATE() WHERE OrganizationId = ? AND DepartmentId = ?",
            ict_sub_id,
            org_id,
            standalone_ict_id,
        )
        moved = cursor.rowcount
        if moved:
            print(f"  Moved {moved} assets from standalone ICT to IT-ICT")

    cursor.execute(
        """
        UPDATE Asset
        SET DepartmentId = ?, UpdatedAt = GETUTCDATE()
        WHERE OrganizationId = ?
          AND DepartmentId IN (SELECT Id FROM Department WHERE OrganizationId = ? AND Code = 'IT' AND ParentDepartmentId IS NULL)
          AND (
                Description LIKE '%Computer Lab%'
             OR Description LIKE '%Comp Lab%'
             OR Description LIKE '%ICT Lab%'
          )
        """,
        ict_sub_id,
        org_id,
        org_id,
    )
    lab_moved = cursor.rowcount
    if lab_moved:
        print(f"  Moved {lab_moved} shared IT lab assets to IT-ICT")

    if ict_all_id is not None:
        cursor.execute("DELETE FROM Department WHERE Id = ?", ict_all_id)
        print("  Removed ICT-ALL department")

    if standalone_ict_id is not None:
        child_count = fetch_one(
            cursor,
            "SELECT COUNT(*) FROM Department WHERE ParentDepartmentId = ?",
            standalone_ict_id,
        )
        asset_count = fetch_one(
            cursor,
            "SELECT COUNT(*) FROM Asset WHERE DepartmentId = ?",
            standalone_ict_id,
        )
        if child_count == 0 and asset_count == 0:
            cursor.execute("DELETE FROM Department WHERE Id = ?", standalone_ict_id)
            print("  Removed standalone ICT department")

    if dry_run:
        print("  (dry run — rolling back)")
        cursor.connection.rollback()
    else:
        cursor.connection.commit()


def main():
    parser = argparse.ArgumentParser(description="Fix IT/ICT department hierarchy.")
    parser.add_argument("--org-id", type=int, action="append", dest="org_ids")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    org_ids = args.org_ids or [10, 11]

    conn = connect()
    cursor = conn.cursor()
    for org_id in org_ids:
        print(f"Org {org_id}:")
        fix_org(cursor, org_id, args.dry_run)
    conn.close()


if __name__ == "__main__":
    main()
