using System;
using System.IO;
using System.IO.Compression;

namespace BitmapIO
{
    class Program
    {
        public static bool debug = false;

        static void Main(string[] args)
        {
            try
            {
                if (args[0] == "debug") //Displays read values and creates extra files that would otherwise be skipped
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

        public static void ExtractBitmap(string FileName) //Extracts a bitmap, decompresses it, creates a DDS header and saves it to the drive for editing
        {

            Console.WriteLine("Enter the Tag Resources offset as an int for the map (found in the Zone tag)"); //These will eventually be replaced by something better
            int TagResource = GetInt();

            Console.WriteLine("Enter the Segments offset as an int for the map (found in the Zone tag)"); //But until I can understand the galaxy brain stuff behind how Assembly reads maps
            int Segments = GetInt();

            Console.WriteLine("Enter the Raw Pages offset as an int for the map (found in the Zone tag)"); //These are here to stay. Ideally the GetInt() method will eventually accept hex though.
            int RawPages = GetInt();

            Console.WriteLine("Enter the Asset Datum Index for the bitmap you want to extract"); //This one will probably stay until I can have it read tag names instead which will be quite a journey.
            int AssetIndex = GetInt();

            int ResourceSegmentOffset = (AssetIndex * 64) + TagResource + 34; //Multiplies the index by the bytes in each block to get to the one we want, then adds 34 so we can get to the 

            FileStream MapFS = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //The map read into a filestream
            byte[] CurrentFile = new byte[MapFS.Length]; //The chosen map read into a byte array
            MapFS.Read(CurrentFile, 0, CurrentFile.Length);

            short SegmentIndex = BitConverter.ToInt16(CurrentFile, ResourceSegmentOffset); //Gets the segment index in tag resources and makes sure it's an int16

            if (debug == true)
            {
                Console.WriteLine("Segment Index: " + SegmentIndex); //Prints the segment index so we can know it has the right one
            }

            int PrimaryPageAddress = Segments + (SegmentIndex * 16); //The primary page index read from segments
            int SecondaryPageAddress = Segments + (SegmentIndex * 16) + 2; //Secondary page index

            short PrimaryPageIndex = BitConverter.ToInt16(CurrentFile, PrimaryPageAddress); //Again, gets those ints and makes them int16
            short SecondaryPageIndex = BitConverter.ToInt16(CurrentFile, SecondaryPageAddress); //These may be unncessary, but better safe than sorry

            if (debug == true)
            {
                Console.WriteLine("Primary Page Index: " + PrimaryPageIndex); //So we know we have the right index again
                Console.WriteLine("Secondary Page Index: " + SecondaryPageIndex);
            }

            int PrimaryRawPageAddress = (PrimaryPageIndex * 88) + RawPages; //The base address for the raw page we're looking for
            int SecondaryRawPageAddress = (SecondaryPageIndex * 88) + RawPages; //The same, but for the secondary raw page

            int PrimaryBlockOffset = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 8); //Like they say on the tin, all of these are reading from the Raw Pages block
            int SecondaryBlockOffset = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 8);
            int PrimaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 12);
            int SecondaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 12);
            int PrimaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 16);
            int SecondaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 16);
            int PrimaryCRCChecksum = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 20);
            int SecondaryCRCChecksum = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 20);

            if (debug == true)
            {
                Console.WriteLine("Primary Block Offset: " + PrimaryBlockOffset); //The above, printed so we can check it all over.
                Console.WriteLine("Secondary Block Offset: " + SecondaryBlockOffset);
                Console.WriteLine("Primary Compressed Block Size: " + PrimaryCompressedBlockSize);
                Console.WriteLine("Secondary Compressed Block Size: " + SecondaryCompressedBlockSize);
                Console.WriteLine("Primary Uncompressed Block Size: " + PrimaryUncompressedBlockSize);
                Console.WriteLine("Secondary Uncompressed Block Size: " + SecondaryUncompressedBlockSize);
                Console.WriteLine("Primary CRC Checksum: " + PrimaryCRCChecksum);
                Console.WriteLine("Secondary CRC Checksum: " + SecondaryCRCChecksum);
            }

            bool PrimaryPageSelected = false;
            bool SecondaryPageSelected = false;

            if (PrimaryPageIndex > 0 && SecondaryPageIndex == 0) PrimaryPageSelected = true; //Each bitmap uses either a primary or secondary page
            if (SecondaryPageIndex > 0 && PrimaryPageIndex == 0) SecondaryPageSelected = true; //This checks which one it has
            while (PrimaryPageIndex > 0 && SecondaryPageIndex > 0 && PrimaryPageSelected == false && SecondaryPageSelected == false) //Some have both, where one page is a mipmap.
            {
                Console.WriteLine("Primary and Secondary pages available. Choose one (secondary recomended if you're not sure)"); //Asks the user which one they want if there's both
                Console.WriteLine("Type 1 for the Primary Page or 2 for the Secondary Page");
                try
                {
                    int answer = Convert.ToInt32(Console.ReadLine()); //Because I just know people will try to type a character instead.
                    switch (answer)
                    {
                        case 1:
                            PrimaryPageSelected = true;
                            break;
                        case 2:
                            SecondaryPageSelected = true;
                            break;
                        default:
                            Console.WriteLine("Please type either 1 or 2");
                            break;
                    }
                }
                catch
                {
                    Console.WriteLine("Please type a number.");
                }
                

            }



            if (SecondaryPageSelected == true)
            {
                Console.WriteLine("Attempting to extract Secondary Page (You will need to keep this in mind when reimporting)");
                MapFS.Seek(SecondaryBlockOffset, 0); //Head over to the offset where our raw dds data starts
                byte[] CompressedSecondaryPageData = new byte[SecondaryCompressedBlockSize]; //A byte array allocated to the size of the compressed data
                MapFS.Read(CompressedSecondaryPageData, 0, SecondaryCompressedBlockSize); //Reads from the stream into the previous byte array, but only the amount of bytes we need
                MapFS.Close(); //Closes the map stream for now

                if (debug == true)
                {
                    Console.WriteLine("Please specify a name for the debug compressed data"); //Outputs the raw compressed data so we can check it over
                    string CompressedFile = Console.ReadLine();
                    File.WriteAllBytes(CompressedFile, CompressedSecondaryPageData);
                }

                byte[] DecompressedSecondaryPageData = new byte[SecondaryUncompressedBlockSize + 128]; //A byte array created with the size of the decompressed data plus the DDS header

                Console.WriteLine("Please specify a filename for the decompressed bitmap"); //Once the data has been decompressed and had the DDS header added, save with this file name
                string OutputFile = Console.ReadLine();

                using MemoryStream SecondaryMemStream = new MemoryStream(CompressedSecondaryPageData); //Create a new memory stream with the compressed data
                {
                    using DeflateStream DFSecondary = new DeflateStream(SecondaryMemStream, CompressionMode.Decompress, true); //Read the memory stream into a deflatestream to decompress it
                    {

                        DFSecondary.Read(DecompressedSecondaryPageData, 128, SecondaryUncompressedBlockSize); //Read the decompressed data into a byte array
                    }
                }
                File.WriteAllBytes(OutputFile, DecompressedSecondaryPageData);
            }
            if (PrimaryPageSelected == true)
            {
                Console.WriteLine("Attempting to extract Primary Page (You will need to keep this in mind when reimporting)");
                MapFS.Seek(PrimaryBlockOffset, 0);
                byte[] CompressedPrimaryPageData = new byte[PrimaryCompressedBlockSize]; //A byte array allocated to the size of the compressed data
                MapFS.Read(CompressedPrimaryPageData, 0, PrimaryCompressedBlockSize); //Reads from the stream into the previous byte array, but only the amount of bytes we need
                MapFS.Close(); //Closes the map stream for now

                if (debug == true)
                {
                    Console.WriteLine("Please specify a name for the debug compressed data"); //Outputs the raw compressed data so we can check it over
                    string CompressedFile = Console.ReadLine();
                    File.WriteAllBytes(CompressedFile, CompressedPrimaryPageData);
                }

                byte[] DecompressedPrimaryPageData = new byte[PrimaryUncompressedBlockSize + 128]; //A byte array created with the size of the decompressed data plus the DDS header

                Console.WriteLine("Please specify a filename for the decompressed bitmap"); //Once the data has been decompressed and had the DDS header added, save with this file name
                string OutputFile = Console.ReadLine();

                using MemoryStream PrimaryMemStream = new MemoryStream(CompressedPrimaryPageData); //Create a new memory stream with the compressed data
                {
                    using DeflateStream DFPrimary = new DeflateStream(PrimaryMemStream, CompressionMode.Decompress, true); //Read the memory stream into a deflatestream to decompress it
                    {

                        DFPrimary.Read(DecompressedPrimaryPageData, 128, PrimaryUncompressedBlockSize); //Read the decompressed data into a byte array
                    }
                }
                File.WriteAllBytes(OutputFile, DecompressedPrimaryPageData);
            }
            else if (PrimaryPageSelected == false && SecondaryPageSelected == false) Console.WriteLine("A page could not be selected");

            /*for (int x = 0; x <= 128; x++) //Writes the DDS header
            {
                 switch(x)
                {
                    default:
                        break;
                    case 0:
                        DecompressedSecondaryPageData[x] = 0x44;
                        break;
                    case 1:
                        DecompressedSecondaryPageData[x] = 0x44;
                        break;
                    case 2:
                        DecompressedSecondaryPageData[x] = 0x53;
                        break;
                    case 3:
                        DecompressedSecondaryPageData[x] = 0x20;
                        break;
                    case 4:
                        DecompressedSecondaryPageData[x] = 0x7C;
                        break;


                }
            }*/


            #region Decompress and save to file
            /*using MemoryStream memStream = new MemoryStream(CompressedPrimaryPageData); //Region could be reused for debugging?
            {
                using FileStream fs2 = File.Create(OutputFile);
                {
                    using DeflateStream ds1 = new DeflateStream(memStream, CompressionMode.Decompress, true);
                    {
                        ds1.CopyTo(fs2);
                    }
                }
            }*/
            #endregion

            /*
            To do: Figure out DDS header creation from scratch, then figure out how to shove it into the byte[] or memory stream 
            040_voi.map

            1947B4DC = tag resources = 424129756

            195F5C3C = segments = 423545900

            19367524 = raw pages = 422999332

            720 = battle rifle texture
            */

        }

        public static void ImportBitmap(string DDSName, string FileName)
        {
            FileStream DDSStream = new FileStream(DDSName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //Reads the input DDS File into a stream
            byte[] DDSBytes = new byte[DDSStream.Length]; //Empty byte array of the same size as the DDS file
            DDSStream.Read(DDSBytes, 0, DDSBytes.Length); //The byte array now contains the bytes from our file. We have to instead use DDSBytes.Length as DDSStream.Length returns a long instead of an Int32
            DDSStream.Close(); //We don't need the DDSStream anymore so we'll close it.

            Console.WriteLine("Enter the Tag Resources offset as an int for the map (found in the Zone tag)"); //These will eventually be replaced by something better
            int TagResource = GetInt();

            Console.WriteLine("Enter the Segments offset as an int for the map (found in the Zone tag)"); //But until I can understand the galaxy brain stuff behind how Assembly reads maps
            int Segments = GetInt();

            Console.WriteLine("Enter the Raw Pages offset as an int for the map (found in the Zone tag)"); //These are here to stay. Ideally the GetInt() method will eventually accept hex though.
            int RawPages = GetInt();

            Console.WriteLine("Enter the Asset Datum Index for the bitmap you want to extract"); //This one will probably stay until I can have it read tag names instead which will be quite a journey.
            int AssetIndex = GetInt();

            int ResourceSegmentOffset = (AssetIndex * 64) + TagResource + 34; //Multiplies the index by the bytes in each block to get to the one we want, then adds 34 so we can get to the 

            FileStream MapFS = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //The map read into a filestream
            byte[] CurrentFile = new byte[MapFS.Length]; //Byte array with the same size as our map
            MapFS.Read(CurrentFile, 0, CurrentFile.Length); //Read the contents of our map into a byte array

            short SegmentIndex = BitConverter.ToInt16(CurrentFile, ResourceSegmentOffset); //Gets the segment index in tag resources and makes sure it's an int16

            if (debug == true)
            {
                Console.WriteLine("Segment Index: " + SegmentIndex); //Prints the segment index so we can know it has the right one
            }

            int PrimaryPageAddress = Segments + (SegmentIndex * 16); //The primary page index read from segments
            int SecondaryPageAddress = Segments + (SegmentIndex * 16) + 2; //Secondary page index

            short PrimaryPageIndex = BitConverter.ToInt16(CurrentFile, PrimaryPageAddress); //Again, gets those ints and makes them int16
            short SecondaryPageIndex = BitConverter.ToInt16(CurrentFile, SecondaryPageAddress); //These may be unncessary, but better safe than sorry

            if (debug == true)
            {
                Console.WriteLine("Primary Page Index: " + PrimaryPageIndex); //So we know we have the right index again
                Console.WriteLine("Secondary Page Index: " + SecondaryPageIndex);
            }

            int PrimaryRawPageAddress = (PrimaryPageIndex * 88) + RawPages; //The base address for the raw page we're looking for
            int SecondaryRawPageAddress = (SecondaryPageIndex * 88) + RawPages; //The same, but for the secondary raw page

            int PrimaryBlockOffset = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 8); //Like they say on the tin, all of these are reading from the Raw Pages block
            int SecondaryBlockOffset = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 8);
            int PrimaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 12);
            int SecondaryCompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 12);
            int PrimaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 16);
            int SecondaryUncompressedBlockSize = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 16);
            int PrimaryCRCChecksum = BitConverter.ToInt32(CurrentFile, PrimaryRawPageAddress + 20);
            int SecondaryCRCChecksum = BitConverter.ToInt32(CurrentFile, SecondaryRawPageAddress + 20);

            if (debug == true)
            {
                Console.WriteLine("Primary Block Offset: " + PrimaryBlockOffset); //The above, printed so we can check it all over.
                Console.WriteLine("Secondary Block Offset: " + SecondaryBlockOffset);
                Console.WriteLine("Primary Compressed Block Size: " + PrimaryCompressedBlockSize);
                Console.WriteLine("Secondary Compressed Block Size: " + SecondaryCompressedBlockSize);
                Console.WriteLine("Primary Uncompressed Block Size: " + PrimaryUncompressedBlockSize);
                Console.WriteLine("Secondary Uncompressed Block Size: " + SecondaryUncompressedBlockSize);
                Console.WriteLine("Primary CRC Checksum: " + PrimaryCRCChecksum);
                Console.WriteLine("Secondary CRC Checksum: " + SecondaryCRCChecksum);
            }

            Console.WriteLine("Inject into primary or secondary page?");
        }



        public static int GetInt() //Attempts to get an int from the user and restarts the loop if it's 0 or not a number
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

        public static string GetFile() //Attempts to get a valid string from the console to use as input
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
