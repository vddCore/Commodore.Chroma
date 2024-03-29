﻿#if !DEBUG
using Commodore.GameLogic.Core.BootSequence;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Chroma.Graphics;
using Chroma.Graphics.TextRendering;
using Chroma.Input;
using Commodore.Framework;
using Commodore.GameLogic.Core.Hardware;
using Commodore.GameLogic.Core.IO;
using Commodore.GameLogic.Core.IO.Storage;
using Commodore.GameLogic.Display;
using Commodore.GameLogic.Executive.CodeEditor;
using Commodore.GameLogic.Executive.CodeEditor.Events;
using Commodore.GameLogic.Interaction;
using Commodore.GameLogic.Network;
using Commodore.GameLogic.Persistence;

namespace Commodore.GameLogic.Core
{
    public partial class Kernel
    {
        private static readonly TrueTypeFont Font;

        static Kernel()
        {
            Font = G.ContentProvider.Load<TrueTypeFont>(
                "Fonts/c64style.ttf",
                16,
                "`1234567890-=qwertyuiop[]asdfghjkl;'\\zxcvbnm,./~!@#$%^&*()_+QWERTYUIOP{}ASDFGHJKL:\"|ZXCVBNM<>?" +
                "\ue05e\ue06a\ue076\ue05f\ue06b\ue077\ue060\ue06c\ue078\ue061\ue06d\ue079\ue062\ue06e\ue07a\ue063" +
                "\ue06f\ue07b\ue064\ue070\ue07c\ue065\ue071\ue07d\ue066\ue072\ue07e\ue067\ue073\ue07f\ue068\ue074" +
                "\ue069\ue075\ue2a0\ue0ac\ue0b8\ue0a1\ue0ad\ue0b9\ue0a2\ue0ae\ue0ba\ue0a3\ue0af\ue0bb\ue0a4\ue0b0" +
                "\ue0bc\ue0a5\ue0b1\ue0bd\ue0a6\ue0b2\ue0be\ue0a7\ue0b3\ue0bf\ue0a8\ue0b4\ue05c\ue0a9\ue0b5\ue0aa" +
                "\ue0b6\ue0ab\ue0b7 "
            );
        }

        private Kernel()
        {
        }

        private static Kernel _instance;
        public static Kernel Instance => _instance ??= new Lazy<Kernel>(() => new Kernel()).Value;

        private Notification _currentNotification;

        public CancellationTokenSource InteractionCancellation { get; private set; }

#if !DEBUG
        public BootSequencePlayer BootSequence;
#endif
        public bool IsRebooting { get; private set; }

        public Memory Memory { get; private set; }

        public VGA Vga;
        public Terminal Terminal;

        public ProcessManager ProcessManager;

        public Editor Editor;
        public Shell Shell;

        public SystemContext CurrentSystemContext { get; set; }
        public SystemContext LocalSystemContext { get; private set; }

        public Queue<Notification> Notifications { get; private set; } = new Queue<Notification>();
        public Stack<Entity> NetworkConnectionStack { get; set; } = new Stack<Entity>();

        public void Reboot(bool isWarmBoot)
        {
            if (_currentNotification != null)
            {
                _currentNotification.IsActive = false;
                _currentNotification = null;
            }

            InteractionCancellation?.Cancel();
            InteractionCancellation = new CancellationTokenSource();

            Notifications.Clear();
            IsRebooting = true;

            if (isWarmBoot)
            {
                if (UserProfile.Instance != null && !UserProfile.Instance.Saving)
                    UserProfile.Instance.SaveToFile();
            }
            else
            {
                UserProfile.Load();
                G.Random = UserProfile.Instance.Random;

                UserProfile.Instance.AutoSave = false;
            }
#if !DEBUG
            BootSequence = new BootSequencePlayer();
#endif

            InitializeSystemMemory();
            InitializeVgaAdapter();
            InitializeIoInterfaces();
            InitializeCodeEditor();
            InitializeShell();
            InitializeCodeExecutionLayer();

            LocalSystemContext = new SystemContext(UserProfile.Instance.RootDirectory);
            CurrentSystemContext = LocalSystemContext;

            if (!isWarmBoot)
            {
                if (UserProfile.Instance.IsInitialized)
                    UserProfile.Instance.AutoSave = true;
            }

            if (isWarmBoot)
            {
                Memory.Poke(SystemMemoryAddresses.SoftResetCompleteFlag, (byte)1);
            }
        }

        public void Draw(RenderContext context)
        {
            if (!Editor.IsVisible)
            {
                Vga.Draw(context);
            }
            else
            {
                Editor.Draw(context);
            }

            if (_currentNotification != null)
                _currentNotification.Draw(context);
        }

        public void Update(float delta)
        {
            if (Notifications.Count > 0 && _currentNotification == null)
            {
                _currentNotification = Notifications.Dequeue();
                _currentNotification.IsActive = true;
            }

            if (_currentNotification != null)
            {
                _currentNotification.Update(delta);

                if (!_currentNotification.IsActive)
                    _currentNotification = null;
            }

            if (Editor.IsVisible)
                Editor.Update(delta);

            Vga.Update(delta);

            Memory.Poke(
                SystemMemoryAddresses.ShiftPressState,
                Keyboard.IsKeyDown(KeyCode.LeftShift) || Keyboard.IsKeyDown(KeyCode.RightShift)
            );

            Memory.Poke(
                SystemMemoryAddresses.CtrlPressState,
                Keyboard.IsKeyDown(KeyCode.LeftControl) || Keyboard.IsKeyDown(KeyCode.RightControl)
            );

            Memory.Poke(
                SystemMemoryAddresses.AltPressState,
                Keyboard.IsKeyDown(KeyCode.LeftAlt) || Keyboard.IsKeyDown(KeyCode.RightAlt)
            );

            Terminal.Update(delta);
        }

        public void Notify(string text)
            => Notify(text, Color.White, Color.Black, Color.White);

        public void Notify(string text, Color borderColor, Color backgroundColor, Color textColor)
            => Notifications.Enqueue(new Notification(text, borderColor, backgroundColor, textColor));

        private void InitializeSystemMemory()
        {
            if (Memory == null)
            {
                Memory = new Memory(SystemConstants.MemorySize);
            }
            else
            {
                Memory.Clear();
            }

            Memory.Poke(SystemMemoryAddresses.BreakKeyScancode, (byte)UserProfile.Instance.PreferredBreakKey);
            Memory.Poke(SystemMemoryAddresses.RevertToTextModeScancode, (byte)UserProfile.Instance.GfxModeResetKey);
            Memory.Poke(SystemMemoryAddresses.MarginArea, (int)0x01010101);
            Memory.Poke(SystemMemoryAddresses.UpdateOffsetParametersFlag, (byte)1);
            Memory.Poke(
                SystemMemoryAddresses.CurrentMarginColor,
                unchecked((int)0xFF00FF00)
            ); // ABGR

            Memory.Poke(
                SystemMemoryAddresses.CurrentForegroundColor,
                unchecked((int)Color.Gray.PackedValue)
            );

            Memory.Poke(
                SystemMemoryAddresses.CurrentBackgroundColor,
                unchecked((int)0xFF000000)
            );

            Memory.Poke(SystemMemoryAddresses.CursorPositionX, 0);
            Memory.Poke(SystemMemoryAddresses.CursorPositionY, 0);
            Memory.Poke(SystemMemoryAddresses.CtrlPressState, (byte)0);
            Memory.Poke(SystemMemoryAddresses.ShiftPressState, (byte)0);
            Memory.Poke(SystemMemoryAddresses.AltPressState, (byte)0);
            Memory.Poke(SystemMemoryAddresses.SoftResetCompleteFlag, (byte)0);
        }

        private void InitializeVgaAdapter()
        {
            if (Vga == null)
            {
                Vga = new VGA(Font);

                Vga.InitialSetupComplete = () =>
                {
                    try
                    {
                        Task.Run(VGA_InitialSetUpComplete, InteractionCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("lol..");
                    }
                };
                Vga.FailsafeTriggered += VGA_FailsafeTriggered;
            }
            else
            {
                Vga.ResetInitialization();
                Vga.ClearScreen(false);
            }
        }

        private void InitializeIoInterfaces()
        {
            // Terminal?.CancelInput();
            Terminal?.InputHistory?.Clear();
            Terminal = null;

            Terminal = new Terminal(Vga);
        }

        private void InitializeCodeExecutionLayer()
        {
            if (ProcessManager == null)
            {
                ProcessManager = new ProcessManager();
            }
            else
            {
                ProcessManager.KillAll();
            }
        }

        private void InitializeCodeEditor()
        {
            if (Editor == null)
            {
                Editor = new Editor(Font);
                Editor.FileSaved += Editor_FileSaved;
            }
            else
            {
                Editor.Reset();
            }
        }

        private void InitializeShell()
        {
            if (Shell == null)
                Shell = new Shell();
        }

        private void VGA_FailsafeTriggered(object sender, EventArgs e)
        {
            Terminal.WriteLine("abnormal vga parameters triggered a failsafe reset");
        }

        private async Task VGA_InitialSetUpComplete()
        {
#if !DEBUG
            BootSequence.Build("RegularBoot");
            await BootSequence.TryPerformSequence();
#endif
            IsRebooting = false;

            if (!UserProfile.Instance.IsInitialized)
            {
                await TextInterface.RunProfileConfigWizard(InteractionCancellation.Token);
                return;
            }

            StartNetworkUpdates();

            await TryRunSystemProgram("/etc/startup", InteractionCancellation.Token);

            DoPostBootActions();

            while (!InteractionCancellation.IsCancellationRequested)
            {
                var customPromptSuccess = await TryRunSystemProgram("/etc/prompt", InteractionCancellation.Token);

                if (!customPromptSuccess)
                {
                    Terminal.Write(
                        $"[{GetHostName()}] | {CurrentSystemContext.WorkingDirectory.GetAbsolutePath()}\n$ ");
                }
    
                try
                {
                    var input = Terminal.ReadLine(string.Empty, InteractionCancellation.Token);
                    
                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    await Shell.HandleCommand(input, InteractionCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("task canceled");
                    return;
                }
            }
        }

        private void DoPostBootActions()
        {
        }

        private async Task<bool> TryRunSystemProgram(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!File.Exists(path))
                return false;

            try
            {
                var pid = ProcessManager.ExecuteProgram(
                    Encoding.UTF8.GetString(File.Get(path).Data),
                    path,
                    null,
                    token
                );

                await ProcessManager.WaitForProgram(pid, InteractionCancellation.Token);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void KeyPressed(KeyCode keyCode, KeyModifiers modifiers)
        {
            if (keyCode == KeyCode.F4)
            {
                Notify("lorem ipsum dolor sit amet\nonsectetur adipisici elit\n\nensign dont pay me enough");
                Notify("to remove this shit");
                Notify("from tech preview");
                Notify("so enjoy the toasts, clients");
                Notify("om-nom-nom");
            }

            if (Editor != null && !Editor.IsVisible)
            {
                if ((byte)keyCode == Memory.Peek8(SystemMemoryAddresses.BreakKeyScancode))
                {
                    if (Memory.PeekBool(SystemMemoryAddresses.CtrlPressState))
                    {
                        ProcessManager.KillAll();
                        return;
                    }
                    else if (Memory.PeekBool(SystemMemoryAddresses.ShiftPressState) && !IsRebooting)
                    {
                        if (NetworkConnectionStack.Any())
                        {
                            Notify("CANNOT REBOOT: CONNECTED TO REMOTE NET_ENTITY");
                            return;
                        }

                        Reboot(true);
                        return;
                    }
                    else
                    {
                        if (Shell.ForegroundPid != null)
                        {
                            ProcessManager.Kill(Shell.ForegroundPid.Value);
                        }
                    }
                }

                Terminal.KeyPressed(keyCode, modifiers);
                Vga.Cursor.Reset();
            }
            else
            {
                Editor?.KeyPressed(keyCode, modifiers);
            }
        }

        public void TextInput(char character)
        {
            if (IsRebooting)
                return;

            if (!Editor.IsVisible)
            {
                Terminal.TextInput(character);
            }
            else
            {
                Editor.TextInput(character);
            }
        }

        private void Editor_FileSaved(object sender, FileSavedEventArgs e)
        {
            var file = File.Create(e.FilePath, true);
            file.SetData(e.Contents);

            if (e.FilePath.StartsWith("/bin/"))
                file.Attributes = FileAttributes.Executable;
        }
    }
}