using System;
using Databases.Model.DatabaseObject;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.TypeComparers;
using StringHelper = Databases.Interpreter.Utility.Helper.StringHelper;

namespace Databases.Manager.Compare
{
    public class TableColumnComparer : BaseTypeComparer
    {
        public TableColumnComparer(RootComparer rootComparer) : base(rootComparer)
        { }

        public override void CompareType(CompareParms parms)
        {
            var column1 = (TableColumn)parms.Object1;
            var column2 = (TableColumn)parms.Object2;

            if (!IsEquals(column1, column2))
            {
                AddDifference(parms);
            }
        }

        private bool IsEquals(TableColumn column1, TableColumn column2)
        {
            if (column1.Name != column2.Name
                || column1.DataType != column2.DataType
                || column1.IsNullable != column2.IsNullable
                || column1.IsIdentity != column2.IsIdentity
                || column1.MaxLength != column2.MaxLength
                || column2.Precision != column2.Precision
                || column2.Scale != column2.Scale
                || column1.Comment != column2.Comment
               )
            {
                return false;
            }

            if (!IsEqualsWithParenthesis(column1.DefaultValue, column2.DefaultValue)
                || !IsEqualsWithParenthesis(column1.ComputeExp, column2.ComputeExp)
               )
            {
                return false;
            }

            return true;
        }

        private bool IsEqualsWithParenthesis(string value1, string value2)
        {
            return StringHelper.GetBalanceParenthesisTrimedValue(value1) ==
                   StringHelper.GetBalanceParenthesisTrimedValue(value2);
        }

        public override bool IsTypeMatch(Type type1, Type type2)
        {
            return type1 == type2 && type1 == typeof(TableColumn);
        }
    }
}