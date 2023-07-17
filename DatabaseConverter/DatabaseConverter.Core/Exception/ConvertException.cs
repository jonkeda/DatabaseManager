using System;
using System.Text;

namespace DatabaseConverter.Core
{
    public abstract class ConvertException : Exception
    {
        protected ConvertException(Exception ex)
        {
            BaseException = ex;
        }

        public Exception BaseException { get; set; }
        public abstract string ObjectType { get; }

        public string SourceServer { get; set; }
        public string SourceDatabase { get; set; }
        public string SourceObject { get; set; }

        public string TargetServer { get; set; }
        public string TargetDatabase { get; set; }
        public string TargetObject { get; set; }

        public override string Message => BaseException.Message;

        public override string StackTrace
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine($"ObjectType:{ObjectType}");
                sb.AppendLine($"SourceServer:{SourceServer}");
                sb.AppendLine($"SourceDatabase:{SourceDatabase}");

                if (!string.IsNullOrEmpty(SourceObject)) sb.AppendLine($"SourceObject:{SourceObject}");

                sb.AppendLine($"TargetServer:{TargetServer}");
                sb.AppendLine($"TargetDatabase:{TargetDatabase}");

                if (!string.IsNullOrEmpty(TargetObject)) sb.AppendLine($"TargetObject:{TargetObject}");

                if (!string.IsNullOrEmpty(BaseException?.StackTrace)) sb.AppendLine(BaseException?.StackTrace);

                return sb.ToString();
            }
        }
    }
}