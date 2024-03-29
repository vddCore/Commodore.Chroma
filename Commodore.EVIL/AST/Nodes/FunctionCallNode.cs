﻿using Commodore.EVIL.AST.Base;
using System.Collections.Generic;

namespace Commodore.EVIL.AST.Nodes
{
    public class FunctionCallNode : AstNode
    {
        public string FunctionName { get; }
        public List<AstNode> Parameters { get; }

        public FunctionCallNode(string functionName, List<AstNode> parameters)
        {
            FunctionName = functionName;
            Parameters = parameters;
        }
    }
}