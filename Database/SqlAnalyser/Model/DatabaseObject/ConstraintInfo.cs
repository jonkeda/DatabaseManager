using System.Collections.Generic;

namespace SqlAnalyser.Model
{
    public class ConstraintInfo
    {
        private ForeignKeyInfo _fk;
        private NameToken _name;
        public ConstraintType Type { get; set; } = ConstraintType.None;

        public NameToken Name
        {
            get => _name;
            set
            {
                _name = value;

                if (value != null)
                {
                    _name.Type = TokenType.ConstraintName;
                }
            }
        }

        public List<ColumnName> ColumnNames { get; set; }

        public ForeignKeyInfo ForeignKey
        {
            get => _fk;
            set
            {
                if (value != null)
                {
                    Type = ConstraintType.ForeignKey;
                }

                _fk = value;
            }
        }

        public TokenInfo Definition { get; set; }
    }

    public enum ConstraintType
    {
        None,
        PrimaryKey,
        ForeignKey,
        UniqueIndex,
        Check,
        Default
    }
}