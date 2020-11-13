using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static NicheRPG.UserInterface;



/*
    OLD drop down console made for an XNA/MONO project, unusable without tinkering cause it uses an UI reference from that project.
    But included just for safekeeping.
*/
namespace NicheRPG
{
    public enum ConsoleCommands
    {
        Invalid = 0, Print, Load, Save
    }

    public enum ConsoleState
    {
        Closed, SlightlyOpen, FullyOpen
    }

    public class DevConsole : UIEntity, IDrawable
    {
        //static reference console instanceen, jota global debuglog funktio käyttää
        static DevConsole dConsole { get; set; }


        SpriteFont font;
        public InputBox commandline;
        Panel bgpanel;

        //bg = tausta, fg = teksti
        Color bgcolor = Color.CornflowerBlue;
        Color fgcolor = Color.Black;


        // The log of console messages
        /*
        */
        Vector2 backlogpos = Vector2.Zero;
        StringBuilder backlog;
        int loglinecount = 0;
        int linespacing = 0;
        int maxlinelength = 256; //not accurate
        const int MAX_LINECOUNT = 10;
        //lasketaan konsolen pohjasta ylöspäin sen verran mitä meillä on linecount
        int LogYPosition { get { return (DrawDestination.Y + Height - linespacing) - (loglinecount * linespacing); } }

        //Opening and closing the panel
        /* 
         * openness meinaa kuinka auki console on verrattuna screen korkeuteen, i.e screenheight * openness
         * max openness on vaa constant ettei se voi koskaan olla yli esim 70% ruudusta
         * openness on vähän meme variable nimi mutta olkoon
         * 
         * deltapersecond on kuinka paljon se aukeaa (per second koska se kerrotaan elapsedtime seconds)
         * esim jos se on 1f niin se aukaa 100% ruudun koosta sekuntissa, eli koska max_openness on 0.7 se aukaa 0.7 sekunnissa
        */
        const float MAX_OPENNESS = 0.7f;
        float openness = 0f;
        float opennesstarget = 0f;
        float deltapersecond = 2f;

        //size of the console & draw position
        /* Width on itsestään selvä mutta height on sen mukaan mikä max_openness on, eli 0.7*ruudun korkeus
         * mikä tekee siitä resolution independent
         * 
         * Yoffset katotaan kuinka open ollaan tällä hetkellä, ja se lisätään DrawDestination neliön y positioniin
         */
        int Width { get { return GameWindow.ScreenRectangle.Width; } }
        int Height { get { return (int)(GameWindow.ScreenRectangle.Height * MAX_OPENNESS); } }
        int YOffset { get { return (int)(GameWindow.ScreenRectangle.Height * openness); } }
        Rectangle DrawDestination { get { return new Rectangle(0, 0 - Height + YOffset, Width, Height); } }

        //state of the console
        ConsoleState state;
        public bool Visible { get; set; }

        public DevConsole()
        {
            font = DefaultFont;

            bgpanel.PanelSprite = UntexturedSprite;
            bgpanel.Color = bgcolor;

            Rectangle cmdlinedestination = new Rectangle(
                0
                , 0
                , Width
                , font.LineSpacing);

            commandline = new InputBox(256);
            commandline.Init("", 256, cmdlinedestination, visible: true);
            commandline.SetAcceptInput(ParseInput);

            linespacing = font.LineSpacing;

            //max capacityn vois laskea jotenki ja se pitäs handlea, tuo 1000 on aika paljon mutta
            //se firee exceptionin jos sen yli mennään
            //tän vois tehä ehkä string[] hommana, nii voi line kohtasesti vaihtaa väriä yms
            backlog = new StringBuilder(1000, 1000);
            Log("Console initialized succesfully!", true);
            Log("This doesn't go in the cmd");
            Log("This goes in the cmd", true);

            dConsole = this;
        }


        public void ParseInput(string t)
        {
            //string[] args = t.Split(' ');
            Log(t, indent: true);
        }

        //static log jolla voi "printtaa" tekstiä console logiin mistä vaan
        //oispa global funktioita
        public void Log(string t, bool logtocmd = false, bool indent = false)
        {
            {   //newline
                backlog.Append("\n");
                if (indent)
                    backlog.Append("  >"); //erottaa oman console input textin logissa
                loglinecount += 1;
            }
            backlog.Append(t);

            if (logtocmd)
                Console.WriteLine(t);
        }

        //global log funktio
        public static void DebugLog(string t, bool logtocmd = false, bool indenttext = false)
        {
            dConsole.Log(t, logtocmd, indenttext);
        }

        ConsoleCommands ParseCommand(char[] chars)
        {
            return ConsoleCommands.Invalid;
        }

        public override void Update(GameTime gt)
        {
            Visible = (openness > 0);


            float delta = (float)gt.ElapsedGameTime.TotalSeconds;

            if (OpeningOrClosing())
                UpdateOpenness(delta);

            if (!Visible)
                return;
            commandline.Update(gt);
        }

        public bool OpeningOrClosing()
        {
            return (openness != opennesstarget);
        }

        public void UpdateOpenness(float dt)
        {
            float dopen = dt * deltapersecond;

            if (openness < opennesstarget)
            {
                openness += dopen;
                //tän vois clampata tai lerpata _mutta_ tää if block on hyödyllinen haluaa lisätä funktionalityä sillonku target on reached
                if (openness > opennesstarget)
                {
                    openness = opennesstarget;
                    if (opennesstarget == MAX_OPENNESS)
                        state = ConsoleState.FullyOpen;
                    else
                        state = ConsoleState.SlightlyOpen;
                }
            }
            else if (openness > opennesstarget)
            {
                openness -= dopen;
                if (openness < opennesstarget)
                {
                    openness = opennesstarget;
                    //unsubscribe keyeventeistä ja state closed (niin peli saa input focusin takas)
                    if (opennesstarget == 0)
                    {
                        GameWindow.kbDispatch.Subscriber = null;
                        state = ConsoleState.Closed;
                    }
                    else
                        state = ConsoleState.SlightlyOpen;
                }
            }

            commandline.SetPosition(DrawDestination.X, DrawDestination.Y + Height);

        }

        public void ToggleConsole(ConsoleState targetstate)
        {
            //Visible = !Visible;

            switch (targetstate)
            {
                case ConsoleState.SlightlyOpen:
                    {
                        if (state == targetstate)
                        {
                            //jos ollaan jo oikeassa state nii suljetaan
                            opennesstarget = 0f;
                            deltapersecond = 2f;

                        }
                        else
                        {
                            opennesstarget = 0.2f;
                            deltapersecond = 2f;
                        }
                    }
                    break;
                case ConsoleState.FullyOpen:
                    {
                        if (state == targetstate)
                        {
                            //jos ollaan jo oikeassa state nii suljetaan
                            opennesstarget = 0f;
                            deltapersecond = 2f;
                        }
                        else
                        {
                            opennesstarget = MAX_OPENNESS;
                            deltapersecond = 4f;

                        }
                    }
                    break;
            }

            Console.WriteLine($"Current state: {state.ToString()} -> target state: {targetstate.ToString()}");

        }

        public void Draw(SpriteBatch batch)
        {
            if (!Visible)
                return;

            backlogpos.Y = LogYPosition;

            bgpanel.Draw(batch, DrawDestination);
            batch.DrawString(DefaultFont, backlog, backlogpos, fgcolor, 0f, Vector2.UnitY, 1f, SpriteEffects.None, 0f);
            commandline.Draw(batch);
        }


    }





}
