﻿using Commodore.EVIL.AST.Base;
using Commodore.EVIL.AST.Nodes;
using Commodore.EVIL.Exceptions;
using Commodore.EVIL.Lexical;

namespace Commodore.EVIL.Parsing
{
    public partial class Parser
    {
        private AstNode MemorySet()
        {
            var memoryCell = MemoryGet();

            if (Scanner.State.CurrentToken.Type == TokenType.Assign)
            {
                Match(TokenType.Assign);
                return new MemorySetNode(memoryCell, Comparison()) { Line = memoryCell.Line };
            }
            else if (Scanner.State.CurrentToken.Type == TokenType.Increment)
            {
                Match(TokenType.Increment);
                return new MemorySetNode(memoryCell, MemorySetNode.MemorySetOperation.PostIncrement) { Line = memoryCell.Line };
            }
            else if (Scanner.State.CurrentToken.Type == TokenType.Decrement)
            {
                Match(TokenType.Decrement);
                return new MemorySetNode(memoryCell, MemorySetNode.MemorySetOperation.PostDecrement) { Line = memoryCell.Line };
            }
            else throw new ParserException($"Expected an assignment, '++' or '--'; got {Scanner.State.CurrentToken.Value}'.", Scanner.State);
        }
    }
}
