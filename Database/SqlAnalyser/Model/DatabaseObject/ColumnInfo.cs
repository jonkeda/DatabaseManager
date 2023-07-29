using System.Collections.Generic;
using Databases.SqlAnalyser.Model.Token;
using SqlAnalyser.Model;

namespace Databases.SqlAnalyser.Model.DatabaseObject
{
    public class ColumnInfo
    {
        private TokenInfo _dataType;
        public ColumnName Name { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsNullable { get; set; } = true;

        public TokenInfo DataType
        {
            get => _dataType;
            set
            {
                _dataType = value;

                if (value != null)
                {
                    _dataType.Type = TokenType.DataType;
                }
            }
        }

        public TokenInfo DefaultValue { get; set; }
        public TokenInfo ComputeExp { get; set; }

        public bool IsComputed => ComputeExp != null;
        public List<ConstraintInfo> Constraints { get; set; }
    }
}