using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace BitmapIO
{
    class Program
    {
        public static bool debug = false;

        static void Main(string[] args)
        {
            try
            {
                if (args[0] == "debug")
                {
                    debug = true;
                    Console.WriteLine("Debug enabled");
                }
            }
            catch
            {

            }
            Console.WriteLine("Enter the name of the map you wish to extract a bitmap from");
            ExtractBitmap(GetFile());

            
        }

        public static void ExtractBitmap(string FileName)
        {

            Console.WriteLine("Enter the Tag Resources offset as an int for the map (found in the Zone tag)");
            int TagResource = GetInt();

            Console.WriteLine("Enter the Segments offset as an int for the map (found in the Zone tag)");
            int Segments = GetInt();

            Console.WriteLine("Enter the Raw Pages offset as an int for the map (found in the Zone tag)");
            int RawPages = GetInt();

            Console.WriteLine("Enter the Asset Datum Index for the bitmap you want to extract");
            int AssetIndex = GetInt();

            int ResourceOffset = (AssetIndex * 64) + TagResource + 34;

            byte[] CurrentFile = File.ReadAllBytes(FileName);

            short SegmentIndex = BitConverter.ToInt16(CurrentFile, ResourceOffset);

            if (debug == true)
            {
                Console.WriteLine("Segment Index: " + SegmentIndex); //The stupid bastard works now
            }

            int PrimaryPageAddress = Segments + (SegmentIndex * 16);
            int SecondaryPageAddress = Segments + (SegmentIndex * 16) + 2;

            short PrimaryPageIndex = BitConverter.ToInt16(CurrentFile, PrimaryPageAddress);
            short SecondaryPageIndex = BitConverter.ToInt16(CurrentFile, SecondaryPageAddress);

            if (debug == true)
            {
                Console.WriteLine("Primary Page Index: " + PrimaryPageIndex);
                Console.WriteLine("Secondary Page Index: " + SecondaryPageIndex);
            }

            int PrimaryRawPageAddress = (PrimaryPageIndex * 88) + RawPages;
            int SecondaryRawPageAddress = (SecondaryPageIndex * 88) + RawPages;

            int PrimaryBlockOffset = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 8);
            int SecondaryBlockOffset = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 8);
            int PrimaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 12);
            int SecondaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 12);
            int PrimaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 16);
            int SecondaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 16);
            int PrimaryCRCChecksum = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 20);
            int SecondaryCRCChecksum = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 20);

            if (debug == true)
            {
                Console.WriteLine("Primary Block Offset: " + PrimaryBlockOffset);
                Console.WriteLine("Secondary Block Offset: " + SecondaryBlockOffset);
                Console.WriteLine("Primary Compressed Block Size: " + PrimaryCompressedBlockSize);
                Console.WriteLine("Secondary Compressed Block Size: " + SecondaryCompressedBlockSize);
                Console.WriteLine("Primary Uncompressed Block Size: " + PrimaryUncompressedBlockSize);
                Console.WriteLine("Secondary Uncompressed Block Size: " + SecondaryUncompressedBlockSize);
                Console.WriteLine("Primary CRC Checksum: " + PrimaryCRCChecksum);
                Console.WriteLine("Secondary CRC Checksum: " + SecondaryCRCChecksum);
            }

            FileStream MapFS = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            MapFS.Seek(PrimaryBlockOffset, 0);
            byte[] CompressedPrimaryPageData = new byte[PrimaryCompressedBlockSize]; //YOU NEED TO SPECIFY THE BYTE SIZE OR IT WON'T HAVE ROOM YOU DUMBASS
            MapFS.Read(CompressedPrimaryPageData, 0, PrimaryCompressedBlockSize);
            MapFS.Close();

            if (debug == true)
            {
                Console.WriteLine("Please specify a name for the debug compressed data");
                string CompressedFile = Console.ReadLine();
                File.WriteAllBytes(CompressedFile, CompressedPrimaryPageData);
            }

            Console.WriteLine("Please specify a filename for the decompressed bitmap");
            string OutputFile = Console.ReadLine();

            using MemoryStream memStream = new MemoryStream(CompressedPrimaryPageData);
            {
                using FileStream fs2 = File.Create(OutputFile);
                {
                    using DeflateStream ds1 = new DeflateStream(memStream, CompressionMode.Decompress, true);
                    {
                        ds1.CopyTo(fs2);
                    }
                }
            }

            /*
            To do: Figure out DDS header creation from scratch, then figure out how to shove it into the byte[] or memory stream 
            1947B4DC = tag resources = 424129756

            195F5C3C = segments = 423545900

            19367524 = raw pages = 422999332
            */

        }

        public static byte[] GetData(FileStream FileName, int Size, int Offset, int Length)
        {
            byte[] buffer = new byte[Size];
            FileName.Read(buffer, Offset, Length);
            return buffer;
        }

        public static void CloseFile(FileStream FileName)
        {
            FileName.Close();
        }

        public static int GetInt()
        {
            int myInt = 0;
            bool assigned = false;
            while (assigned == false)
            {
                try
                {
                    myInt = Convert.ToInt32(Console.ReadLine());
                }
                catch
                {
                    Console.WriteLine("Please specify a numeric value above 0");
                    continue;
                }
                if (myInt == 0)
                {
                    Console.WriteLine("Please specify a numeric value above 0");
                    continue;
                }
                if (myInt > 0)
                {
                    assigned = true;
                }
            }
            return myInt;
        }

        public static string GetFile()
        {
            string MapToRead = "null";
            bool validname = false;
            while (validname == false)
            {
                try
                {
                    MapToRead = Console.ReadLine();
                }
                catch
                {
                    Console.WriteLine("Please specify a valid filename.");
                    continue;
                }
                if (MapToRead != "null")
                {
                    validname = true;
                }
                if (MapToRead == "null")
                {
                    continue;
                }

            }
            return MapToRead;
        }
    }
}
