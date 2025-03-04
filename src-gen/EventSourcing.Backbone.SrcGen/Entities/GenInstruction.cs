﻿namespace EventSourcing.Backbone.SrcGen.Entities
{
    internal class GenInstruction
    {
        public GenInstruction(string file, string content, string? ns = null, params string[] usingAddition)
        {
            File = file;
            Content = content;
            NS = ns;
            UsingAddition = usingAddition.Select(m => m.StartsWith("using") ? m : $"using {m};").ToArray();
        }

        public string File { get; }
        public string Content { get; }
        public string? NS { get; }
        public string[] UsingAddition { get; }

        public void Deconstruct(
            out string file,
            out string content,
            out string? ns,
            out string[] usingAddition)
        {
            file = File;
            content = Content;
            ns = NS;
            usingAddition = UsingAddition;
        }
    }
}
