using System.Collections.Generic;
using Databases.Model.DatabaseObject;

namespace Databases.Model.Schema
{
    public class SchemaInfo
    {
        public List<UserDefinedType> UserDefinedTypes { get; set; } = new List<UserDefinedType>();
        public List<Sequence> Sequences { get; set; } = new List<Sequence>();
        public List<DatabaseObject.Function> Functions { get; set; } = new List<DatabaseObject.Function>();
        public List<Table> Tables { get; set; } = new List<Table>();
        public List<View> Views { get; set; } = new List<View>();
        public List<TableTrigger> TableTriggers { get; set; } = new List<TableTrigger>();
        public List<Procedure> Procedures { get; set; } = new List<Procedure>();
        public List<TableColumn> TableColumns { get; set; } = new List<TableColumn>();
        public List<TablePrimaryKey> TablePrimaryKeys { get; set; } = new List<TablePrimaryKey>();
        public List<TableForeignKey> TableForeignKeys { get; set; } = new List<TableForeignKey>();
        public List<TableIndex> TableIndexes { get; set; } = new List<TableIndex>();
        public List<TableConstraint> TableConstraints { get; set; } = new List<TableConstraint>();
    }
}