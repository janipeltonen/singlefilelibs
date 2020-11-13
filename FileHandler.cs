using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;

/*
    Unnecessarily complex (manually written) binary level file writer/reader.
    This was practice for binary files and file layouts (also super fast :o)
*/


namespace FileHandler
{
    
    /*
     * Tänne systeemi jolla voi kirjottaa binary filuun leveldatan jonka voi myöhemmin editorissa ja ingame
     * streamata current level dataan
     */
    public class LevelWriter : IDisposable
    {
        readonly BinaryWriter Writer;
        readonly object synclock = new object();

        private bool headerwritten = false;

        public LevelWriter(Stream outstream)
        {
            if (outstream == null)
                return;

            Writer = new BinaryWriter(outstream);
        }

        void WriteHeader(Stream source, BinaryWriter w, in Span<char> name, int levelwidth, int levelheight)
        {
            //header, kaikki level filut alkaa tällä, jos filua lukiessa käy ilmi ettei header matchaa, abandonataan filu
            w.Write("JANI".ToCharArray()); //4 byte
            w.Write(name.Length); //4 byte kokonen integer nimi stringin pituudesta
            w.Write(name.ToArray()); //i hope this works
            w.Write(levelwidth); //4 byte, levelin width
            w.Write(levelheight); //4 byte
            w.Write((byte)0); //terminator 0 loppuun, header finished
        }

        static void WriteLayerData(Stream source, BinaryWriter w, in Span<byte> layertiles)
        {
            //otetaan ylös kuinka monta tileä pitää kirjottaa
            //ja laitetaan se tieto ylös filuun ennen datablock
            int datalength = layertiles.Length;
            w.Write(datalength); //4 byte
            //sitten kirjotetaan tilet
            for (int i = 0; i < datalength; i++)
            {
                byte tile = layertiles[i];
                w.Write(tile);  //byte per tile (duh)
            }
        }

        public void WriteLevel(in LevelDataStackOnly data)
        {
            lock (synclock)
            {
                using (var stream = new MemoryStream())
                {
                    if (!headerwritten)
                        WriteHeader(stream, Writer, in data.name, data.levelwidth, data.levelheight);
                    //kirjotetaan tilet
                    WriteLayerData(stream, Writer, in data.layer);
                    //todo: kirjota entityt, esim monsterit ja regionit
                }
            }

            if (!headerwritten)
                headerwritten = true;
        }

        public void Dispose()
        {
            Writer.Write((byte)0x4b);
            Writer.BaseStream.Dispose();
            Writer.Dispose();
        }
    }

    public class LevelReader : IDisposable
    {
       
        BinaryReader Reader;
        int datalength;
        byte[] data;
        bool validfile = false;
        int offset = 0;

        public LevelReader(Stream stream)
        {
            if (stream == null)
                return;

            Reader = new BinaryReader(stream);
        }

        public LevelReader(string filename)
            : this(new FileStream(filename, FileMode.Open)) { }



        public bool ReadFile(Stream input, ref LevelDataStackOnly leveldata)
        {
            bool validfile = false;

            datalength = (int)input.Length;
            data = new byte[datalength];
            Reader.Read(data, 0, datalength);

            string identifier = Encoding.UTF8.GetString(data, 0, 4);
            if (!ValidFile(identifier))
                return false;

            
            //FILE HEADER
            {
                offset += 4; //offsetataan heti 4 koska identifier on 4 byteä ja ei tarvi lukea sitä
                int namelength = BitConverter.ToInt32(data, offset);
                offset += 4;
                leveldata.name = new Span<char>(Encoding.UTF8.GetChars(data, offset, namelength));
                offset += namelength;
                leveldata.levelwidth = BitConverter.ToInt32(data, offset);
                offset += 4;
                leveldata.levelheight = BitConverter.ToInt32(data, offset);
                offset += 5; //hypätään samalla terminaattorin yli + int32 length
            }
            //TILE LAYER
            {
                int tilecount = BitConverter.ToInt32(data, offset);
                offset += 4;
                //leveldata.layer määritetään ennenku leveldata ees syötetään tälle funktiolle
                //niin flushataan ne ensin
                for (int i = 0; i < leveldata.layer.Length; i++)
                    leveldata.layer[i] = 0;
                //sitten luetaan ne datasta
                for (int i = 0; i < tilecount; i++)
                {
                    leveldata.layer[i] = data[offset];
                    offset += 1;
                }
            }

            return validfile;
        }

        bool ValidFile(string headerid) => (headerid == "JANI");

        public void Dispose()
        {
            Reader.BaseStream.Dispose();
            Reader.Dispose();
        }
    }

    //DEPRECATED
    /*
    public static class StructUtility
    {


        //public static T RawDeserialize<T>(byte[] rawData, int position)
        //{
        //    int rawsize = Marshal.SizeOf(typeof(T));
        //    if (rawsize > rawData.Length - position)
        //        throw new ArgumentException("Not enough data to fill struct. Array length from position: " + (rawData.Length - position) + ", Struct length: " + rawsize);
        //    IntPtr buffer = Marshal.AllocHGlobal(rawsize);
        //    Marshal.Copy(rawData, position, buffer, rawsize);
        //    T retobj = (T)Marshal.PtrToStructure(buffer, typeof(T));
        //    Marshal.FreeHGlobal(buffer);
        //    return retobj;
        //}

        /*
        public static byte[] Serialize(object anything)
        {
            int rawSize = Marshal.SizeOf(anything);
            IntPtr buffer = Marshal.AllocHGlobal(rawSize);
            Marshal.StructureToPtr(anything, buffer, false);
            byte[] rawDatas = new byte[rawSize];
            Marshal.Copy(buffer, rawDatas, 0, rawSize);
            Marshal.FreeHGlobal(buffer);
            //
            Console.WriteLine($"Serializing object: {anything.ToString()} complete. Array size: {rawSize.ToString()}");
            return rawDatas;
        }

        //serialize T struct (polymorphic, eli voit pass minkä vaa struct tälle ja se palauttaa typen T)
        public static T Deserialize<T>(byte[] array)
            where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var ptr = Marshal.AllocHGlobal(size);
            T t;
            try
            {
                Marshal.Copy(array, 0, ptr, size);
            }
            finally
            {
                t = (T)Marshal.PtrToStructure(ptr, typeof(T));
                Marshal.FreeHGlobal(ptr);
            }
            return t;

        }
    }
         */
}
