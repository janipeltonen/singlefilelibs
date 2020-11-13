using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

/*
An improved input handling class for XNA/Mono framework

Handles events such as holding buttons better than the default state classes.

For ease of use, it's a static object that you initialize on startup and in the beginning of your gameloop you just read the input and handle however you see fit.

WIN32 hooks included in the end of the file for typing and manually reading windows key events.
*/
namespace XNAInputHandler
{

    public struct Input
    {
        static KeyboardState kbstate;
        static KeyboardState lastkbstate;
        static MouseState mousestate;
        static MouseState lastmousestate;
        static int lastscrollvalue;

        //MOUSE STATES
        public static Vector2 MousePosition;
        public static bool ScrollUp() => mousestate.ScrollWheelValue > lastscrollvalue;
        public static bool ScrollDown() => mousestate.ScrollWheelValue < lastscrollvalue;
        public static bool LeftMouseClicked()
        {
            return mousestate.LeftButton == ButtonState.Pressed &&
                lastmousestate.LeftButton == ButtonState.Released;
        }
        public static bool LeftMouseHeld()
        {
            return mousestate.LeftButton == ButtonState.Pressed &&
                lastmousestate.LeftButton == ButtonState.Pressed;
        }
        public static bool LeftMouseReleased()
        {
            return mousestate.LeftButton == ButtonState.Released &&
                lastmousestate.LeftButton == ButtonState.Pressed;
        }
        public static bool RightMouseClicked()
        {
            return mousestate.RightButton == ButtonState.Pressed &&
                lastmousestate.RightButton == ButtonState.Released;
        }
        public static bool RightMouseHeld()
        {
            return mousestate.RightButton == ButtonState.Pressed &&
                lastmousestate.RightButton == ButtonState.Pressed;
        }
        public static bool RightMouseReleased()
        {
            return mousestate.RightButton == ButtonState.Released &&
                lastmousestate.RightButton == ButtonState.Pressed;
        }

        //KEYBOARD STATES
        public static bool KeyReleased(Keys key) => kbstate.IsKeyUp(key) && lastkbstate.IsKeyDown(key);
        public static bool KeyPressed(Keys key) => kbstate.IsKeyDown(key) && lastkbstate.IsKeyUp(key);
        public static bool KeyDown(Keys key) => kbstate.IsKeyDown(key);
        public static bool KeyHeld(Keys key) => kbstate.IsKeyDown(key) && lastkbstate.IsKeyDown(key);

        public void Init()
        {
            kbstate = Keyboard.GetState();
            mousestate = Mouse.GetState();
            lastkbstate = kbstate;
            lastmousestate = mousestate;
            MousePosition = new Vector2(mousestate.X, mousestate.Y);
            lastscrollvalue = mousestate.ScrollWheelValue;
        }

        public void ReadInput()
        {
            //take last frames data
            lastkbstate = kbstate;
            lastmousestate = mousestate;
            lastscrollvalue = mousestate.ScrollWheelValue;
            //get new values
            kbstate = Keyboard.GetState();
            mousestate = Mouse.GetState();
            MousePosition = new Vector2(mousestate.X, mousestate.Y);
        }

    }
    
    //dispatcher handlaa lowlevel kommunikaation windows keyboard hook kanssa ja lähettää ne inputit Ikeyboardlistenerille joka on sen hetkinen subscriber
    public class KeyboardDispatcher
    {
        public KeyboardDispatcher(Microsoft.Xna.Framework.GameWindow window)
        {
            InputEventSystem.EventInput.Initialize(window);
            InputEventSystem.EventInput.CharEntered += new InputEventSystem.CharEnteredHandler(EventInput_CharEntered);
            InputEventSystem.EventInput.KeyDown += new InputEventSystem.KeyEventHandler(EventInput_KeyDown);
        }

        void EventInput_KeyDown(object sender, InputEventSystem.KeyEventArgs e)
        {
            if (_subscriber == null)
                return;

            _subscriber.ReceiveSpecialInput(e.KeyCode);
        }

        void EventInput_CharEntered(object sender, InputEventSystem.CharacterEventArgs e)
        {
            if (_subscriber == null)
                return;
            if (char.IsControl(e.Character))
            {
                //ctrl-v
                if (e.Character == 0x16)
                {
                    //XNA runs in Multiple Thread Apartment state, which cannot recieve clipboard
                    Thread thread = new Thread(PasteThread);
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    _subscriber.ReceiveInput(_pasteResult);
                }
                else
                {
                    _subscriber.ReceiveCommand(e.Character);
                }
            }
            else
            {
                _subscriber.ReceiveInput(e.Character);
            }
        }

        IKeyboardListener _subscriber;
        public IKeyboardListener Subscriber
        {
            get { return _subscriber; }
            set
            {
                if (_subscriber != null)
                    _subscriber.Selected = false;
                _subscriber = value;
                if (value != null)
                    value.Selected = true;
            }
        }

        //Thread has to be in Single Thread Apartment state in order to receive clipboard
        string _pasteResult = "";
        [STAThread]
        void PasteThread()
        {
            if (Clipboard.ContainsText())
            {
                _pasteResult = Clipboard.GetText();
            }
            else
            {
                _pasteResult = "";
            }
        }
    }

    //interface jonka kautta voi vastaanottaa dispatcherin inputteja jotka tulee suoraa windows keyeventeistä
    //eli jos haluat lukea kirjotusta/clipboard täytyy käyttää tätä ja subscribe se dispatcheriin
    public interface IKeyboardListener
    {
        void ReceiveInput(char c);
        void ReceiveInput(string text);
        void ReceiveCommand(char cmd);
        void ReceiveSpecialInput(Keys key);

        bool Selected { get; set; }
    }
}

namespace InputEventSystem
{

    public class KeyboardLayout
    {
        const uint KLF_ACTIVATE = 1; //activate the layout
        const int KL_NAMELENGTH = 9; // length of the keyboard buffer
        const string LANG_EN_US = "00000409";
        const string LANG_HE_IL = "0001101A";

        [DllImport("user32.dll")]
        private static extern long LoadKeyboardLayout(
              string pwszKLID,  // input locale identifier
              uint Flags       // input locale identifier options
              );

        [DllImport("user32.dll")]
        private static extern long GetKeyboardLayoutName(
              System.Text.StringBuilder pwszKLID  //[out] string that receives the name of the locale identifier
              );

        public static string getName()
        {
            System.Text.StringBuilder name = new System.Text.StringBuilder(KL_NAMELENGTH);
            GetKeyboardLayoutName(name);
            return name.ToString();
        }
    }

    public class CharacterEventArgs : EventArgs
    {
        private readonly char character;
        private readonly int lParam;

        public CharacterEventArgs(char character, int lParam)
        {
            this.character = character;
            this.lParam = lParam;
        }

        public char Character
        {
            get { return character; }
        }

        public int Param
        {
            get { return lParam; }
        }

        public int RepeatCount
        {
            get { return lParam & 0xffff; }
        }

        public bool ExtendedKey
        {
            get { return (lParam & (1 << 24)) > 0; }
        }

        public bool AltPressed
        {
            get { return (lParam & (1 << 29)) > 0; }
        }

        public bool PreviousState
        {
            get { return (lParam & (1 << 30)) > 0; }
        }

        public bool TransitionState
        {
            get { return (lParam & (1 << 31)) > 0; }
        }
    }

    public class KeyEventArgs : EventArgs
    {
        private Keys keyCode;

        public KeyEventArgs(Keys keyCode)
        {
            this.keyCode = keyCode;
        }

        public Keys KeyCode
        {
            get { return keyCode; }
        }
    }

    public delegate void CharEnteredHandler(object sender, CharacterEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);

    public static class EventInput
    {
        
        public static event CharEnteredHandler CharEntered;
        public static event KeyEventHandler KeyDown;
        public static event KeyEventHandler KeyUp;

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static bool initialized;
        static IntPtr prevWndProc;
        static WndProc hookProcDelegate;
        static IntPtr hIMC;

        //various Win32 constants that we need
        const int GWL_WNDPROC = -4;
        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_CHAR = 0x102;
        const int WM_IME_SETCONTEXT = 0x0281;
        const int WM_INPUTLANGCHANGE = 0x51;
        const int WM_GETDLGCODE = 0x87;
        const int WM_IME_COMPOSITION = 0x10f;
        const int DLGC_WANTALLKEYS = 4;

        //Win32 functions that we're using
        [DllImport("Imm32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("Imm32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /*
         *  jos haluaa compilea x64 nii vaihtaa dwNewLong (long) eikä (int)
         *  sama pitää tehä init funktion Marshal.GetFunctionPointerForDelegate conversionissa tai tulee overflow
         */
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);


        public static void Initialize(GameWindow window)
        {
            if (initialized)
                throw new InvalidOperationException("TextInput.Initialize can only be called once!");

            hookProcDelegate = new WndProc(HookProc);
            prevWndProc = (IntPtr)SetWindowLong(window.Handle, GWL_WNDPROC,
                (long)Marshal.GetFunctionPointerForDelegate(hookProcDelegate));

            hIMC = ImmGetContext(window.Handle);
            initialized = true;
        }

        static IntPtr HookProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            IntPtr returnCode = CallWindowProc(prevWndProc, hWnd, msg, wParam, lParam);

            switch (msg)
            {
                case WM_GETDLGCODE:
                    returnCode = (IntPtr)(returnCode.ToInt32() | DLGC_WANTALLKEYS);
                    break;

                case WM_KEYDOWN:
                    if (KeyDown != null)
                        KeyDown(null, new KeyEventArgs((Keys)wParam));
                    break;

                case WM_KEYUP:
                    if (KeyUp != null)
                        KeyUp(null, new KeyEventArgs((Keys)wParam));
                    break;

                case WM_CHAR:
                    if (CharEntered != null)
                        CharEntered(null, new CharacterEventArgs((char)wParam, lParam.ToInt32()));
                    break;

                case WM_IME_SETCONTEXT:
                    if (wParam.ToInt32() == 1)
                        ImmAssociateContext(hWnd, hIMC);
                    break;

                case WM_INPUTLANGCHANGE:
                    ImmAssociateContext(hWnd, hIMC);
                    returnCode = (IntPtr)1;
                    break;
            }

            return returnCode;
        }
    }
}
