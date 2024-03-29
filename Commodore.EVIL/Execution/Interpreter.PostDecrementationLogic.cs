﻿using Commodore.EVIL.Abstraction;
using Commodore.EVIL.AST.Nodes;
using Commodore.EVIL.Exceptions;
using System.Diagnostics;

namespace Commodore.EVIL.Execution
{
    public partial class Interpreter
    {
        public override DynValue Visit(PostDecrementationNode postDecrementationNode)
        {
            DynValue retVal = null;
            var name = postDecrementationNode.Variable.Name;

            if (CallStack.Count > 0)
            {
                var stackTop = CallStack.Peek();

                if (stackTop.ParameterScope.ContainsKey(name))
                {
                    retVal = stackTop.ParameterScope[name].Copy();
                    stackTop.ParameterScope[name] = new DynValue(retVal.Number - 1);
                }
                else if (stackTop.LocalVariableScope.ContainsKey(name))
                {
                    retVal = stackTop.LocalVariableScope[name].Copy();
                    stackTop.LocalVariableScope[name] = new DynValue(retVal.Number - 1);
                }
                else if (Environment.Globals.ContainsKey(name))
                {
                    retVal = Environment.Globals[name].Copy();
                    Environment.Globals[name] = new DynValue(retVal.Number - 1);
                }
                else
                {
                    throw new RuntimeException($"The referenced variable '{name}' was never defined.",
                        postDecrementationNode.Line);
                }
            }
            else
            {
                if (!Environment.Globals.ContainsKey(name))
                    throw new RuntimeException($"The referenced variable '{name}' was never defined.",
                        postDecrementationNode.Line);

                retVal = Environment.Globals[name].Copy();
                Environment.Globals[name] = new DynValue(retVal.Number - 1);
            }

            Debug.Assert(retVal != null, "PostDecrementation -- internal interpreter failure: retVal == null?!");
            return retVal;
        }
    }
}