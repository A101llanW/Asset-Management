using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace AssetManagement.Infrastructure.Persistence
{
    internal sealed class SqlQueryable<T> : IOrderedQueryable<T>, IQueryProvider
    {
        private readonly Func<Expression, object> _executor;

        public SqlQueryable(Func<Expression, object> executor)
            : this(executor, null)
        {
        }

        public SqlQueryable(Func<Expression, object> executor, Expression expression)
        {
            _executor = executor;
            Expression = expression ?? Expression.Constant(this);
        }

        public Type ElementType { get { return typeof(T); } }

        public Expression Expression { get; private set; }

        public IQueryProvider Provider { get { return this; } }

        public IEnumerator<T> GetEnumerator()
        {
            var result = Execute(Expression) as IEnumerable<T>;
            if (result == null)
            {
                throw new InvalidOperationException(
                    "SQL query execution did not return a sequence of " + typeof(T).Name + ".");
            }

            return result.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? typeof(T);
            var queryType = typeof(SqlQueryable<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(
                queryType,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new object[] { _executor, expression },
                null);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new SqlQueryable<TElement>(_executor, expression);
        }

        public object Execute(Expression expression)
        {
            return _executor(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)_executor(expression);
        }
    }

    internal static class SqlQueryableExpressionHelper
    {
        public static Expression<Func<T, bool>> TryBuildPredicate<T>(Expression expression, EntityMap map)
        {
            var predicates = new List<LambdaExpression>();
            CollectWherePredicates(expression, predicates);
            if (predicates.Count == 0)
            {
                return null;
            }

            try
            {
                var parameter = Expression.Parameter(typeof(T), "entity");
                Expression body = null;
                foreach (var predicate in predicates)
                {
                    var current = new ParameterReplaceVisitor(predicate.Parameters[0], parameter).Visit(predicate.Body);
                    body = body == null ? current : Expression.AndAlso(body, current);
                }

                var typed = Expression.Lambda<Func<T, bool>>(body, parameter);
                SqlPredicateBuilder.Build(typed, map);
                return typed;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        public static Expression ReplaceRepositoryRoot(Expression expression, IQueryable rows)
        {
            return new RepositoryRootReplaceVisitor(rows).Visit(expression);
        }

        public static object ExecuteInMemory<T>(Expression expression, IList<T> rows)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            if (IsSqlQueryableRoot(expression))
            {
                return rows;
            }

            var queryableRows = rows.AsQueryable();
            var rewritten = ReplaceRepositoryRoot(expression, queryableRows);
            if (typeof(IQueryable).IsAssignableFrom(expression.Type))
            {
                return queryableRows.Provider.CreateQuery(rewritten);
            }

            return queryableRows.Provider.Execute(rewritten);
        }

        public static bool IsSqlQueryableRoot(Expression expression)
        {
            var constant = expression as ConstantExpression;
            if (constant == null || constant.Value == null || !constant.Value.GetType().IsGenericType)
            {
                return false;
            }

            return constant.Value.GetType().GetGenericTypeDefinition() == typeof(SqlQueryable<>);
        }

        private static void CollectWherePredicates(Expression expression, IList<LambdaExpression> predicates)
        {
            var method = expression as MethodCallExpression;
            if (method == null)
            {
                return;
            }

            if (method.Method.Name == "Where" && method.Arguments.Count == 2)
            {
                var lambda = StripQuotes(method.Arguments[1]) as LambdaExpression;
                if (lambda != null)
                {
                    predicates.Add(lambda);
                }
            }

            if ((method.Method.Name == "Any" || method.Method.Name == "Count"
                || method.Method.Name == "First" || method.Method.Name == "FirstOrDefault"
                || method.Method.Name == "Single" || method.Method.Name == "SingleOrDefault")
                && method.Arguments.Count == 2)
            {
                var lambda = StripQuotes(method.Arguments[1]) as LambdaExpression;
                if (lambda != null)
                {
                    predicates.Add(lambda);
                }
            }

            CollectWherePredicates(method.Arguments[0], predicates);
        }

        private static Expression StripQuotes(Expression expression)
        {
            while (expression.NodeType == ExpressionType.Quote)
            {
                expression = ((UnaryExpression)expression).Operand;
            }

            return expression;
        }

        private sealed class ParameterReplaceVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _source;
            private readonly ParameterExpression _target;

            public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
            {
                _source = source;
                _target = target;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _source ? _target : base.VisitParameter(node);
            }
        }

        private sealed class RepositoryRootReplaceVisitor : ExpressionVisitor
        {
            private readonly IQueryable _rows;

            public RepositoryRootReplaceVisitor(IQueryable rows)
            {
                _rows = rows;
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value != null && node.Value.GetType().IsGenericType
                    && node.Value.GetType().GetGenericTypeDefinition() == typeof(SqlQueryable<>))
                {
                    return Expression.Constant(_rows);
                }

                return base.VisitConstant(node);
            }
        }
    }
}
