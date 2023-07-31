﻿namespace Databases.Interpreter.Utility.Model
{
    public delegate void FeedbackHandler(FeedbackInfo feedbackInfo);

    public class FeedbackInfo
    {
        public object Owner { get; set; }
        public FeedbackInfoType InfoType { get; set; }
        public string Message { get; set; }
        public bool IgnoreError { get; set; }
    }

    public enum FeedbackInfoType
    {
        Info = 0,
        Error = 1
    }
}