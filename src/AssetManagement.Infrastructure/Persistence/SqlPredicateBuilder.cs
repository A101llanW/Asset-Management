using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AssetManagement.Infrastructure.Persistence
{
    internal sealed class SqlPredicate
    {
        public string Sql { get; set; }

        public IList<KeyValuePair<string, object>> Parameters { get; private set; }

        public SqlPredicate()
        {
            Parameters = new List<KeyValuePair<string, object>>();
        }
    }

    internal static class SqlPredicateBuilder
    {
        public static SqlPredicate Build<T>(Expression<Func<T, bool>> predicate, EntityMap map)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            var result = new SqlPredicate();
            result.Sql = BuildExpression(predicate.Body, map, result);
            return result;
        }

        private static string BuildExpression(Expression expression, EntityMap map, SqlPredicate result)
        {
            var binary = expression as BinaryExpression;
            if (binary != null)
            {
                if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.And
                    || binary.NodeType == ExpressionType.OrElse || binary.NodeType == ExpressionType.Or)
                {
                    var operation = binary.NodeType == ExpressionType.OrElse || binary.NodeType == ExpressionType.Or
                        ? " OR " : " AND ";
                    return "(" + BuildExpression(binary.Left, map, result) + operation
                        + BuildExpression(binary.Right, map, result) + ")";
                }

                if (binary.NodeType == ExpressionType.Equal || binary.NodeType == ExpressionType.NotEqual
                    || binary.NodeType == ExpressionType.GreaterThan || binary.NodeType == ExpressionType.GreaterThanOrEqual
                    || binary.NodeType == ExpressionType.LessThan || binary.NodeType == ExpressionType.LessThanOrEqual)
                {
                    var member = FindMember(binary.Left, map) ?? FindMember(binary.Right, map);
                    if (member == null)
                    {
                        throw Unsupported(expression);
                    }

                    var valueExpression = member == FindMember(binary.Left, map) ? binary.Right : binary.Left;
                    if (IsNull(valueExpression))
                    {
                        if (binary.NodeType != ExpressionType.Equal && binary.NodeType != ExpressionType.NotEqual)
                        {
                            throw Unsupported(expression);
                        }

                        return "[" + member.Name + "] IS " + (binary.NodeType == ExpressionType.Equal ? "NULL" : "NOT NULL");
                    }

                    var parameterName = AddValue(result, Evaluate(valueExpression));
                    var op = GetOperator(binary.NodeType);
                    return "[" + member.Name + "]" + op + parameterName;
                }
            }

            var unary = expression as UnaryExpression;
            if (unary != null && unary.NodeType == ExpressionType.Not)
            {
                return "NOT (" + BuildExpression(unary.Operand, map, result) + ")";
            }

            var memberExpression = expression as MemberExpression;
            if (memberExpression != null && memberExpression.Member.Name == "HasValue")
            {
                var nullableMember = FindMember(memberExpression.Expression, map);
                if (nullableMember != null)
                {
                    return "[" + nullableMember.Name + "] IS NOT NULL";
                }
            }

            if (memberExpression != null && memberExpression.Type == typeof(bool))
            {
                var member = FindMember(memberExpression, map);
                if (member != null)
                {
                    return "[" + member.Name + "]=1";
                }
            }

            var method = expression as MethodCallExpression;
            if (method != null)
            {
                return BuildMethodCall(method, map, result);
            }

            throw Unsupported(expression);
        }

        private static string BuildMethodCall(MethodCallExpression method, EntityMap map, SqlPredicate result)
        {
            if (method.Method.DeclaringType == typeof(string))
            {
                if (method.Method.Name == "Equals" && method.Arguments.Count == 2)
                {
                    var leftMember = FindMember(method.Arguments[0], map);
                    var rightMember = FindMember(method.Arguments[1], map);
                    if (leftMember != null && rightMember == null)
                    {
                        return "[" + leftMember.Name + "]= " + AddValue(result, Evaluate(method.Arguments[1]));
                    }

                    if (rightMember != null && leftMember == null)
                    {
                        return "[" + rightMember.Name +"]= " + AddValue(result, Evaluate(method.Arguments[0]));
                    }

                    var instanceMember = FindMember(method.Object, map);
                    if (instanceMember != null)
                    {
                        return "[" + instanceMember.Name +"]= " + AddValue(result, Evaluate(method.Arguments[0]));
                    }
                }

                var member = FindMember(method.Object, map);
                if (member == null || method.Arguments.Count != 1)
                {
                    throw Unsupported(method);
                }

                var value = Evaluate(method.Arguments[0]);
                var pattern = method.Method.Name == "StartsWith" ? Convert.ToString(value, CultureInfo.InvariantCulture) + "%"
                    : method.Method.Name == "EndsWith" ? "%" + Convert.ToString(value, CultureInfo.InvariantCulture)
                    : method.Method.Name == "Contains" ? "%" + Convert.ToString(value, CultureInfo.InvariantCulture) + "%"
                    : null;
                if (pattern == null)
                {
                    throw Unsupported(method);
                }

                return "[" + member.Name + "] LIKE " + AddValue(result, pattern);
            }

            if (method.Method.Name == "Contains"
                && (method.Arguments.Count == 1 || method.Arguments.Count == 2))
            {
                var memberExpression = method.Arguments.Count == 2
                    ? method.Arguments[1]
                    : method.Arguments[0];
                var member = FindMember(memberExpression, map);
                Expression valuesExpression;
                if (method.Arguments.Count == 2)
                {
                    valuesExpression = method.Arguments[0];
                }
                else
                {
                    member = FindMember(method.Object, map);
                    valuesExpression = method.Arguments[0];
                }

                if (member == null)
                {
                    throw Unsupported(method);
                }

                var values = Evaluate(valuesExpression) as IEnumerable;
                if (values == null)
                {
                    throw Unsupported(method);
                }

                var names = new List<string>();
                foreach (var value in values)
                {
                    names.Add(AddValue(result, value));
                }

                return names.Count == 0 ? "1=0" : "[" + member.Name + "] IN (" + string.Join(",", names.ToArray()) + ")";
            }

            if (method.Method.Name == "get_HasValue")
            {
                var member = FindMember(method.Object, map);
                if (member != null)
                {
                    return "[" + member.Name + "] IS NOT NULL";
                }
            }

            throw Unsupported(method);
        }

        private static PropertyInfo FindMember(Expression expression, EntityMap map)
        {
            var convert = expression as UnaryExpression;
            if (convert != null && (convert.NodeType == ExpressionType.Convert || convert.NodeType == ExpressionType.ConvertChecked))
            {
                expression = convert.Operand;
            }

            var member = expression as MemberExpression;
            if (member == null || !(member.Member is PropertyInfo) || member.Expression == null)
            {
                return null;
            }

            if (member.Member.Name == "HasValue")
            {
                return null;
            }

            var parameter = member.Expression as ParameterExpression;
            if (parameter == null)
            {
                return null;
            }

            var property = (PropertyInfo)member.Member;
            return map.ScalarProperties.Any(scalar => scalar.Name == property.Name) ? property : null;
        }

        private static bool IsNull(Expression expression)
        {
            return expression is ConstantExpression && ((ConstantExpression)expression).Value == null;
        }

        private static object Evaluate(Expression expression)
        {
            var constant = expression as ConstantExpression;
            if (constant != null)
            {
                return constant.Value;
            }

            var convert = expression as UnaryExpression;
            if (convert != null && convert.NodeType == ExpressionType.Convert)
            {
                return Evaluate(convert.Operand);
            }

            try
            {
                var lambda = Expression.Lambda(expression);
                return lambda.Compile().DynamicInvoke();
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("The repository predicate contains an unsupported value expression.", ex);
            }
        }

        private static string AddValue(SqlPredicate result, object value)
        {
            var name = "@p" + result.Parameters.Count.ToString(CultureInfo.InvariantCulture);
            if (value != null && value.GetType().IsEnum)
            {
                value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            result.Parameters.Add(new KeyValuePair<string, object>(name, value ?? DBNull.Value));
            return name;
        }

        private static string GetOperator(ExpressionType nodeType)
        {
            switch (nodeType)
            {
                case ExpressionType.Equal: return "=";
                case ExpressionType.NotEqual: return "<>";
                case ExpressionType.GreaterThan: return ">";
                case ExpressionType.GreaterThanOrEqual: return ">=";
                case ExpressionType.LessThan: return "<";
                case ExpressionType.LessThanOrEqual: return "<=";
                default: throw new InvalidOperationException("Unsupported comparison operator.");
            }
        }

        private static NotSupportedException Unsupported(Expression expression)
        {
            return new NotSupportedException("The repository predicate contains an unsupported expression: " + expression);
        }
    }
}
