﻿using Commodore.GameLogic.Core;
using Commodore.GameLogic.Core.IO.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Commodore.GameLogic.Interaction.Shell
{
    public class TermShell
    {
        public delegate bool Command(string[] args);

        public Dictionary<string, Command> BuiltIns { get; }

        public TermShell()
        {
            BuiltIns = new Dictionary<string, Command>();

            BuiltIns.Add("edit", (args) =>
            {
                if (args.Length == 0)
                {
                    Kernel.Instance.CodeEditor.Reset();
                    Kernel.Instance.CodeEditor.IsVisible = true;
                }

                if (args.Length > 0)
                    Kernel.Instance.CodeEditor.OpenPath(args[0]);

                return true;
            });
        }

        public bool HandleCommand(string input)
        {
            try
            {
                var tokens = Tokenize(input);

                var command = tokens[0];
                var args = tokens.Skip(1).ToArray();

                if (BuiltIns.ContainsKey(command))
                    return BuiltIns[command](args);

                var filePath = $"/bin/{command}";

                if (command.StartsWith("./") || command.StartsWith("../") || command.StartsWith("/"))
                {
                    filePath = command;
                }

                if (Directory.Exists(filePath))
                {
                    Kernel.Instance.Terminal.WriteLine($"{filePath}: is a directory");
                    return false;
                }

                if (File.Exists(filePath))
                {
                    var file = File.Get(filePath);

                    if ((file.Attributes & FileAttributes.Executable) != 0)
                    {
                        Kernel.Instance.CodeExecutionLayer.ExecuteProgram(file.GetData(), args).GetAwaiter().GetResult();
                        return true;
                    }
                    else
                    {
                        Kernel.Instance.Terminal.WriteLine($"CANNOT EXEC \uFF26{filePath}\uFF40: not an executable");
                        return false;
                    }
                }

                Kernel.Instance.Terminal.WriteLine($"\uFF26UNRECOGNIZED COMMAND AND NO EXECUTABLE FOUND\uFF40\n");
                return false;
            }
            catch (Exception e)
            {
                Kernel.Instance.Terminal.WriteLine($"\uFF24ERROR:\n\uFF40{e.Message}\n");
            }
            return false;
        }

        private List<string> Tokenize(string input)
        {
            var ret = new List<string>();
            var currentToken = string.Empty;

            for (var i = 0; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '\\':
                    {
                        if (i + 1 >= input.Length)
                        {
                            currentToken += '\\';
                        }
                        else
                        {
                            i++;
                            currentToken += input[i];
                        }

                        break;
                    }

                    case '"':
                        if (i + 1 >= input.Length)
                        {
                            currentToken += '"';
                        }
                        else
                        {
                            i++;

                            while (input[i] != '"')
                            {
                                if (i + 1 >= input.Length)
                                    throw new Exception("Unterminated string.");

                                if (input[i] == '\\')
                                {
                                    if (i + 1 >= input.Length)
                                        throw new Exception("Unterminated string.");

                                    i++;
                                    switch (input[i])
                                    {
                                        case '\\':
                                            currentToken += '\\';
                                            i++;
                                            break;

                                        case 'n':
                                            currentToken += '\n';
                                            i++;
                                            break;

                                        case 'r':
                                            currentToken += '\r';
                                            i++;
                                            break;

                                        case '"':
                                            currentToken += '"';
                                            i++;
                                            break;
                                        default: throw new Exception("Unrecognized escape sequence.");
                                    }
                                }
                                else
                                {
                                    currentToken += input[i++];
                                }
                            }
                        }
                        break;

                    case ' ':
                        if (!string.IsNullOrWhiteSpace(currentToken))
                            ret.Add(currentToken);

                        currentToken = string.Empty;
                        break;

                    default:
                        currentToken += input[i];
                        break;
                }
            }

            if (currentToken.Length > 0)
                ret.Add(currentToken);

            return ret;
        }
    }
}