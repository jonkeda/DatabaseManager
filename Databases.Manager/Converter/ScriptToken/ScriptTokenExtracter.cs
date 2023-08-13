using System;
using System.Collections.Generic;
using Databases.SqlAnalyser.Model.DatabaseObject;
using Databases.SqlAnalyser.Model.Script;
using Databases.SqlAnalyser.Model.Statement;
using Databases.SqlAnalyser.Model.Token;

namespace Databases.Converter.ScriptToken
{
    public class ScriptTokenExtracter
    {
        private readonly List<TokenInfo> tokens = new List<TokenInfo>();

        public ScriptTokenExtracter(Statement statement)
        {
            Statement = statement;
        }

        public Statement Statement { get; set; }

        public IEnumerable<TokenInfo> Extract()
        {
            tokens.Clear();

            ExtractTokens(Statement);

            return tokens;
        }

        private void ExtractTokens(dynamic obj, bool isFirst = true)
        {
            if (obj == null)
            {
                return;
            }

            Type type = obj.GetType();

            void ReadProperties()
            {
                var properties = type.GetProperties();

                foreach (var property in properties)
                {
                    if (property.Name == nameof(TokenInfo.Parent))
                    {
                        continue;
                    }

                    var value = property.GetValue(obj);

                    if (value == null)
                    {
                        continue;
                    }

                    if (value is TokenInfo)
                    {
                        if (!value.Equals(obj))
                        {
                            this.ExtractTokens(value, false);
                        }
                    }
                    else if (value.GetType().IsClass && property.PropertyType.IsGenericType &&
                             !(property.DeclaringType == typeof(CommonScript) &&
                               property.Name == nameof(CommonScript.Functions)))
                    {
                        foreach (var v in value)
                        {
                            this.ExtractTokens(v, false);
                        }
                    }
                    else if (value is Statement || value is StatementItem || value is SelectTopInfo ||
                             value is TableInfo || value is ColumnInfo || value is ConstraintInfo ||
                             value is ForeignKeyInfo)
                    {
                        this.ExtractTokens(value, false);
                    }
                }
            }

            if (obj is TokenInfo token)
            {
                AddToken(token);

                ReadProperties();

                return;
            }

            ReadProperties();
        }

        private void AddToken(TokenInfo token)
        {
            if (token == null)
            {
                return;
            }

            tokens.Add(token);
        }
    }
}