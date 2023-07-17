using System;

namespace DatabaseInterpreter.Model
{
    public class SchemaInfoFilter
    {
        public DatabaseObjectType DatabaseObjectType = DatabaseObjectType.None;
        public bool Strict { get; set; }
        public string Schema { get; set; }
        public string[] UserDefinedTypeNames { get; set; }
        public string[] SequenceNames { get; set; }
        public string[] FunctionNames { get; set; }
        public string[] TableNames { get; set; }
        public string[] ViewNames { get; set; }
        public string[] ProcedureNames { get; set; }
        public string[] TableTriggerNames { get; set; }
        public ColumnType ColumnType { get; set; } = ColumnType.TableColumn;
        public bool IsForView { get; set; }
    }

    [Flags]
    public enum DatabaseObjectType
    {
        None = 0,
        Table = 2,
        View = 4,
        Type = 8,
        Function = 16,
        Procedure = 32,
        Column = 64,
        Trigger = 128,
        PrimaryKey = 256,
        ForeignKey = 512,
        Index = 1024,
        Constraint = 2048,
        Sequence = 4096
    }

    public enum ColumnType
    {
        None = 0,
        TableColumn = 2,
        ViewColumn = 4
    }
}