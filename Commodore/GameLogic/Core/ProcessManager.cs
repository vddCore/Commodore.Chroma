﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chroma.Diagnostics.Logging;
using Commodore.Framework;
using Commodore.EVIL.Abstraction;
using Commodore.EVIL.Exceptions;
using Commodore.EVIL.Execution;
using Commodore.GameLogic.Core.Exceptions;
using Commodore.GameLogic.Executive.EvilRuntime;

namespace Commodore.GameLogic.Core
{
    public class ProcessManager
    {
        public const int MaximumProcessCount = 64;

        private readonly string _programTemplate;
        private int _nextPid;

        private Log Log { get; } = LogManager.GetForCurrentAssembly();

        public Dictionary<int, Process> Processes { get; private set; }
        public Stack<DynValue> ReturnValues { get; private set; } = new Stack<DynValue>();

        public ProcessManager()
        {
            if (_programTemplate == null)
            {
                using (var fs = G.ContentProvider.Open("Sources/Templates/program.template.cplx"))
                {
                    using (var sr = new StreamReader(fs))
                        _programTemplate = sr.ReadToEnd();
                }
            }

            Reset();
        }

        public void Reset()
        {
            if (Processes != null)
            {
                KillAll();
            }
            else
            {
                Processes = new Dictionary<int, Process>();
            }

            _nextPid = 0;
            ReturnValues.Clear();
        }

        public void Kill(int pid)
        {
            if (!Processes.ContainsKey(pid))
                return;

            var proc = Processes[pid];
            
            proc.Interpreter.BreakExecution = true;

            foreach (var childProcPid in proc.Children)
            {
                Kill(childProcPid);
            }
            
            if (proc.Parent != null)
                proc.Children.Remove(Processes[pid].Pid);

            Processes.Remove(pid);
        }

        public void KillAll()
        {
            for (var i = 0; i < Processes.Values.Count; i++)
                Kill(i);
        }

        public Process GetProcess(Interpreter interpreter)
        {
            return Processes.Values.FirstOrDefault(p => p.Interpreter == interpreter);
        }

        public int GetPid(Interpreter interpreter)
        {
            var proc = GetProcess(interpreter);

            if (proc == null)
                return -1;

            return proc.Pid;
        }

        public async Task WaitForProgram(int pid, CancellationToken token)
        {
            while (IsProcessRunning(pid))
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1);
            }
        }

        public bool IsProcessRunning(int pid)
        {
            return Processes.ContainsKey(pid);
        }

        public int ExecuteProgram(string code, string filePath, int? parentPid, CancellationToken token,
            params string[] args)
        {
            if (Processes.Count >= MaximumProcessCount)
                return -1;

            var interp = CreateProcess();
            var targetCode = code ?? string.Empty;

            targetCode = _programTemplate.Replace("%prepped_source%", targetCode);

            var argsTable = new Table();
            for (var i = 0; i < args.Length; i++)
                argsTable[i] = new DynValue(args[i]);

            interp.Environment.SupplementLocalLookupTable.Add("args", new DynValue(argsTable));

            var pid = _nextPid++;
            if (parentPid == null)
            {
                Processes.Add(pid, new Process(pid, string.Join(' ', args), interp) {FilePath = filePath});
            }
            else
            {
                var child = new Process(pid, string.Join(' ', args), interp, parentPid) {FilePath = filePath};
                var parent = Processes[parentPid.Value];
                parent.Children.Add(child.Pid);

                Processes.Add(pid, child);
            }

            // We don't want to wait for this here.
#pragma warning disable 4014
            Task.Run(async () =>
#pragma warning restore 4014
            {
                ReturnValues.Push(await ExecuteCode(interp, targetCode, token));
                Kill(pid);
            });

            return pid;
        }

        public async Task<DynValue> ExecuteCode(Interpreter interpreter, string code, CancellationToken token)
        {
            try
            {
                return await interpreter.ExecuteAsync(code, token);
            }
            catch (ScriptTerminationException)
            {
                // Kernel.Instance.Terminal.WriteLine($"\uFF3FBREAK BY USER\uFF40");
            }
            catch (ScannerException se)
            {
                Kernel.Instance.Terminal.WriteLine(
                    $"\uFF24LEXER ERROR | LINE {se.Line}\uFF40\n{se.Message}\n");
            }
            catch (ParserException pe)
            {
                Kernel.Instance.Terminal.WriteLine(
                    $"\uFF24PARSER ERROR | LINE {(pe.ScannerState?.Line)?.ToString() ?? "??"}\uFF40\n{pe.Message}\n");
            }
            catch (RuntimeException re)
            {
                Kernel.Instance.Terminal.WriteLine(
                    $"\uFF24RUNTIME ERROR | LINE {(re.Line - 1).ToString()}\uFF40\n{re.Message}\n");
            }
            catch (ClrFunctionException cfe)
            {
                Kernel.Instance.Terminal.WriteLine($"\uFF24BUILT-IN FUNCTION ERROR\uFF40\n{cfe.Message}\n");
            }
            catch (KernelException ke)
            {
                Kernel.Instance.Terminal.WriteLine($"\uFF24KERNEL ERROR\uFF40\n{ke.Message}\n");
            }
            catch (Exception e)
            {
                Kernel.Instance.Terminal.WriteLine($"\uFF24!!! INTERNAL SYSTEM ERROR !!!\uFF40\n");
                Log.Exception(e);
            }
            finally
            {
                if (interpreter.Environment.SupplementLocalLookupTable.ContainsKey("args"))
                    interpreter.Environment.SupplementLocalLookupTable.Remove("args");
            }

            return DynValue.Zero;
        }

        private Interpreter CreateProcess()
        {
            var interp = new Interpreter();

            interp.Environment.LoadCoreRuntime();
            interp.Environment.RegisterPackage<SysLibrary>();
            interp.Environment.RegisterPackage<MemLibrary>();
            interp.Environment.RegisterPackage<VgaLibrary>();
            interp.Environment.RegisterPackage<FsLibrary>();
            interp.Environment.RegisterPackage<NetLibrary>();

            interp.Memory = Kernel.Instance.Memory;

            return interp;
        }
    }
}