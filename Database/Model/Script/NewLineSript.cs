using System;

namespace DatabaseInterpreter.Model
{
    public class NewLineSript : Script
    {
        public NewLineSript()
        {
            Content = Environment.NewLine;
        }
    }
}