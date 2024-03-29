﻿using Commodore.EVIL.AST.Nodes;
using Commodore.EVIL.Lexical;

namespace Commodore.EVIL.Parsing
{
    public partial class Parser
    {
        private VariableNode Variable(string identifier = null)
        {
            var name = identifier;
            var line = Scanner.State.Line;

            if (identifier == null)
            {
                name = (string)Scanner.State.CurrentToken.Value;
                line = Match(TokenType.Identifier);
            }

            return new VariableNode(name) { Line = line };
        }
    }
}
