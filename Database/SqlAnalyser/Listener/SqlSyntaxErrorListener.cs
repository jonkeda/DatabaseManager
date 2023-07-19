using System.IO;
using Antlr4.Runtime;
using SqlAnalyser.Model;

namespace SqlAnalyser.Core
{
    public class SqlSyntaxErrorListener : BaseErrorListener
    {
        public bool HasError => Error != null && Error.Items.Count > 0;
        public SqlSyntaxError Error { get; private set; }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line,
            int charPositionInLine, string msg, RecognitionException e)
        {
            if (Error == null) Error = new SqlSyntaxError();

            if (offendingSymbol is CommonToken token)
            {
                var errorItem = new SqlSyntaxErrorItem();

                errorItem.StartIndex = token.StartIndex;
                errorItem.StopIndex = token.StopIndex;
                errorItem.Line = token.Line;
                errorItem.Column = token.Column + 1;
                errorItem.Text = token.Text;
                errorItem.Message = msg;

                Error.Items.Add(errorItem);
            }

            base.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
        }
    }
}