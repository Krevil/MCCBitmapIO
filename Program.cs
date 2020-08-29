using System;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq;

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
            Console.WriteLine("Extract or import?");
            string response = Console.ReadLine();
            switch (response.ToLower())
            {
                case "extract":
                    Console.WriteLine("Enter the name of the map you wish to extract a bitmap from");
                    ExtractBitmap(GetFile());
                    break;
                case "import":
                    Console.WriteLine("Enter the name of the image file and then the map you wish to import it to");
                    ImportBitmap(GetFile(), GetFile());
                    break;
                default:
                    Console.WriteLine("Please type either extract or import");
                    break;


            }
        }

        public static void ReadMap(string mapName) //actual main thing
        {
            FileStream MapFS = new FileStream(mapName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //opens the target map in a filestream
            /*Console.WriteLine("What offset would you like to get?");
            string inputoffset = Console.ReadLine();
            long offset = long.Parse(inputoffset, NumberStyles.HexNumber);*/

            //File Header
            MapFS.Seek(0x4B4 + 0x8, 0); //mask offset
            byte[] maskoffset = new byte[0x4];
            MapFS.Read(maskoffset, 0, 4);

            MapFS.Seek(0x4C4 + 0x10, 0); //sections offset
            byte[] sectionoffset = new byte[0x4];
            MapFS.Read(sectionoffset, 0, 4);

            MapFS.Seek(0x2F8, 0); //virtual base
            byte[] virtualbaseaddress = new byte[0x8];
            MapFS.Read(virtualbaseaddress, 0, 8);

            MapFS.Seek(0x10, 0); //index header address
            byte[] indexheaderaddress = new byte[0x8];
            MapFS.Read(indexheaderaddress, 0, 8);

            int tag_section_offset = BitConverter.ToInt32(maskoffset, 0) + BitConverter.ToInt32(sectionoffset, 0);

            long address_mask = tag_section_offset - BitConverter.ToInt64(virtualbaseaddress, 0);

            long tags_header_offset = BitConverter.ToInt64(indexheaderaddress, 0) + address_mask;


            //Index table header
            MapFS.Seek(tags_header_offset, 0); //tags header offset
            byte[] group_tag_count = new byte[0x4];
            MapFS.Read(group_tag_count, 0, 4);

            MapFS.Seek(tags_header_offset + 0x8, 0);
            byte[] tag_group_table_address = new byte[0x8];
            MapFS.Read(tag_group_table_address, 0, 8);

            MapFS.Seek(tags_header_offset + 0x10, 0);
            byte[] tag_count = new byte[0x4];
            MapFS.Read(tag_count, 0, 4);

            MapFS.Seek(tags_header_offset + 0x18, 0);
            byte[] tag_table_address = new byte[0x8];
            MapFS.Read(tag_table_address, 0, 8);

            long tag_group_table_address_real = BitConverter.ToInt64(tag_group_table_address) + address_mask;

            long tag_table_address_real = BitConverter.ToInt64(tag_table_address) + address_mask;

            //Tag group table
            MapFS.Seek(tag_group_table_address_real, 0);
            byte[] tag_group_magic = new byte[0x4];
            MapFS.Read(tag_group_magic, 0, 4);

            //Tag table
            MapFS.Seek(tag_table_address_real, 0); //int16
            byte[] tag_group_index = new byte[0x2];
            MapFS.Read(tag_group_index, 0, 2);

            int ZoneTagGroup = ZoneGroupTagLookUp(MapFS, tag_group_table_address_real, group_tag_count);

            ZoneTagLookUp(MapFS, address_mask, tag_table_address_real, tag_count, ZoneTagGroup);

        }
        public static string ArrayToHex(byte[] array) //Converts the contents of an array to a hexadecimal string
        {
            uint myInt = BitConverter.ToUInt32(array);
            return myInt.ToString("X");
        }
        public static string ArrayToString(byte[] array) //Converts the contents of an array to a numerical string
        {
            uint myInt = BitConverter.ToUInt32(array);
            return myInt.ToString();
        }
        public static int TagLookUp(FileStream MapFS, long tag_table_address_real, int tagIndex)
        {
            long IndexOffset = tag_table_address_real + (tagIndex * 0x8);
            for (long i = tag_table_address_real; i <= IndexOffset; i += 0x8) //loops through each element in the tag table looking for the tag index
            {

                if (i == IndexOffset)
                {
                    MapFS.Seek(i, 0);
                    byte[] group_magic = new byte[0x2]; //magic means tag type? idk
                    MapFS.Read(group_magic, 0, 2);
                    byte[] datumindexsalt = new byte[0x2];
                    MapFS.Read(datumindexsalt, 0, 2);
                    byte[] memoryaddress = new byte[0x4];
                    MapFS.Read(memoryaddress, 0, 4);
                    //Console.WriteLine("Tag group index is: {0}", BitConverter.ToString(group_magic));
                    return BitConverter.ToInt32(memoryaddress);
                }
            }
            return 0;
        }
        public static int ZoneGroupTagLookUp(FileStream MapFS, long tag_group_table_address_real, byte[] group_tag_count)
        {
            int iterations = 0;
            for (long i = tag_group_table_address_real; i < tag_group_table_address_real + (0x10 * BitConverter.ToInt32(group_tag_count)); i += 0x10) //loops through each element in the group tag table looking for zone
            {
                byte[] enoz = { 0x65, 0x6E, 0x6F, 0x7A }; //zone but backwards because endian i guess
                MapFS.Seek(i, 0); //has to use i and i has to hhe the tag table address. dont think too much about it, it's just simpler this way
                byte[] group_magic = new byte[0x4]; //magic means tag type i guess
                MapFS.Read(group_magic, 0, 4); //read for whatever reason equates to "store this thing in a byte array"
                if (BitConverter.ToInt32(enoz) == BitConverter.ToInt32(group_magic)) //this really should work without the bitconversion but fuck it
                {
                    Console.WriteLine("Zone is: {0}", iterations);
                    return iterations;
                }
                iterations++; //has to be at the end (after the return) or you end up getting the wrong number
            }
            return 0; //needs a return 0 or c# won't like it
        }
        public static long ZoneTagLookUp(FileStream MapFS, long address_mask, long tag_table_address_real, byte[] tag_count, int ZoneTagGroup)
        {
            for (long i = tag_table_address_real; i < tag_table_address_real + (0x8 * BitConverter.ToInt32(tag_count)); i += 0x8) //loops through each element in the tag table looking for the group index
            {
                MapFS.Seek(i, 0); //has to use i and i has to be the the tag table address. dont think too much about it
                byte[] group_index = new byte[0x2]; //first thing in the tag table is the group index ie if it's a vehicle tag it'll be vehi
                MapFS.Read(group_index, 0, 2);
                int group_index_int = BitConverter.ToInt16(group_index); //so we can compare it to our zone tag group we found from zonegrouptaglookup
                if (group_index_int == ZoneTagGroup)
                {
                    byte[] tag_datum = new byte[0x2]; //probably unneeded
                    MapFS.Read(tag_datum, 0, 2);
                    byte[] tag_memory_address = new byte[0x4]; //this is the thing we really want to go to where the actual tag is in the file
                    MapFS.Read(tag_memory_address, 0, 4);
                    long tag_memory_address_real = GetTagAddress(tag_memory_address, address_mask); //tag and block addresses have to be multiplied by 4 before applying the address mask
                    //Console.WriteLine("Zone Tag Group: {0} \nTag Datum: {1} \nTag Address {2}", BitConverter.ToString(group_index), BitConverter.ToUInt16(tag_datum), tag_memory_address_real); //yummy debug stuff
                    return tag_memory_address_real; //now the method returns the tag address so we can do fun stuff with it
                }
            }
            return 0;
        }
        public static long ReadZone(FileStream MapFS, long address_mask, long tag_memory_address_real, long tag_table_address_real, int AssetIndex)
        {
            MapFS.Seek(tag_memory_address_real + 0x64, 0); //Go to tag offset plus the location of Tag Resources
            byte[] TagResourceCount = new byte[0x4];
            MapFS.Read(TagResourceCount, 0, 4); //Gets the number of entries in tag resources
            int TagResourceCount_int = BitConverter.ToInt32(TagResourceCount);
            byte[] TagResourceAddress = new byte[0x4];
            MapFS.Read(TagResourceAddress, 0, 4); //Gets the virtual address for the block
            long TagResourceAddress_real = GetTagAddress(TagResourceAddress, address_mask);

            MapFS.Seek(tag_memory_address_real + 0x58, 0); //Go to tag offset plus the location of Segments
            byte[] SegmentsCount = new byte[0x4];
            MapFS.Read(SegmentsCount, 0, 4); //Gets the number of entries in segments
            int SegmentsCount_int = BitConverter.ToInt32(SegmentsCount);
            byte[] SegmentsAddress = new byte[0x4];
            MapFS.Read(SegmentsAddress, 0, 4); //Gets the virtual address for the block
            long SegmentsAddress_real = GetTagAddress(SegmentsAddress, address_mask);

            MapFS.Seek(tag_memory_address_real + 0x34, 0); //Go to tag offset plus the location of raw pages
            byte[] RawPagesCount = new byte[0x4];
            MapFS.Read(RawPagesCount, 0, 4); //Gets the number of entries in raw pages
            int RawPagesCount_int = BitConverter.ToInt32(RawPagesCount);
            byte[] RawPagesAddress = new byte[0x4];
            MapFS.Read(RawPagesAddress, 0, 4); //Gets the virtual address for the block
            long RawPagesAddress_real = GetTagAddress(SegmentsAddress, address_mask);

            //Now we have all we need from Zone for now

            MapFS.Seek(TagResourceAddress_real + (0xA4 * AssetIndex) + 0xC, 0); //Go to the tag resource block plus the Asset Index times the size of the block and then to the datum index
            byte[] Datum = new byte[0x4]; //We skip the parent group because we don't care what it is. Though it wouldn't be a bad idea to check if it is indeed mtib (bitm).
            MapFS.Read(Datum, 0, 4);

            int DatumIndex = BitConverter.ToInt32(Datum) & 0xFFFF; //Tag index. Don't ask. I don't know.
            int TagMemoryAddress = TagLookUp(MapFS, tag_table_address_real, DatumIndex); //Gets the memory address for the referenced tag

            MapFS.Seek(TagMemoryAddress + 0x60, 0); //Now we go to our tag from zone, then to it's tagblock for bitmaps so we can get the juicy information inside
            byte[] BitmapsCount = new byte[0x4]; //should just be 1 but idk. 
            MapFS.Read(BitmapsCount, 0, 4); 
            byte[] BitmapsAddress = new byte[0x4];
            MapFS.Read(BitmapsAddress, 0, 4); //Gets the virtual address for the block
            long BitmapsAddress_real = GetTagAddress(BitmapsAddress, address_mask);

            MapFS.Seek(BitmapsAddress_real + 0x4, 0); //And finally into the bitmaps block to get the needed information for the DDS header, starting at the Width because why not
            byte[] BitmapWidthArray = new byte[0x2];
            MapFS.Read(BitmapWidthArray, 0, 2);
            byte[] BitmapHeightArray = new byte[0x2];
            MapFS.Read(BitmapHeightArray, 0, 2);
            byte[] BitmapDepthArray = new byte[0x1];
            MapFS.Read(BitmapDepthArray, 0, 1); //Idk about this. Also after this is more shit idk so for now we skip em and go straight to the format enums
            MapFS.Seek(BitmapsAddress_real + 0xC, 0);
            byte[] BitmapFormat = new byte[0x2];
            MapFS.Read(BitmapFormat, 0, 2); //this is an enum. 16 is DXT5 which is the ordinary but could be others.
            byte[] BitmapFlags = new byte[0x2];
            MapFS.Read(BitmapFlags, 0, 2); //Oh boy. This is gonna be fun to figure out. Like an enum but with multiple choice. (Actually not that bad, it's just addition)
            
            /*TO DO: Merge the methods into Extract Bitmap */

            return 0;
        }
        public static long GetTagAddress(byte[] tag_memory_address, long address_mask) //specifically for tags and tagblocks because of the int32 and * 4 requirement
        {
            long tag_memory_address_int = BitConverter.ToInt32(tag_memory_address); //have to multiply the virtual address by 4. don't know why, just do it
            long tag_memory_address_4x = tag_memory_address_int * 4; //but you can't do it on the same line. again, dunno why.
            long tag_memory_address_real = tag_memory_address_4x + address_mask; //now you can add the address mask
            return tag_memory_address_real; //now this is the real address.
        }

        //All the old stuff below here

        public static void ExtractBitmap(string FileName) //Extracts a bitmap, decompresses it, creates a DDS header and saves it to the drive for editing
        {
            FileStream MapFS = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //The map read into a filestream

            byte[] MapBytes = new byte[MapFS.Length]; //The chosen map read into a byte array
            MapFS.Read(MapBytes, 0, MapBytes.Length);

            byte[] tagbufferoffsetArray = new byte[0x4];
            MapFS.Seek(0x18, 0);
            MapFS.Read(tagbufferoffsetArray, 0, 4);

            Console.WriteLine("Enter the Tag Resources offset for the map (found in the Zone tag)"); //These will eventually be replaced by something better
            int TagResource = GetInt();

            Console.WriteLine("Enter the Segments offset for the map (found in the Zone tag)"); //But until I can understand the galaxy brain stuff behind how Assembly reads maps
            int Segments = GetInt();

            Console.WriteLine("Enter the Raw Pages offset for the map (found in the Zone tag)"); //These are here to stay. Ideally the GetInt() method will eventually accept hex though.
            int RawPages = GetInt();

            Console.WriteLine("Enter the Asset Datum Index for the bitmap you want to extract"); //This one will probably stay until I can have it read tag names instead which will be quite a journey.
            int AssetIndex = GetInt();

            int ResourceSegmentOffset = (AssetIndex * 64) + TagResource + 34; //Multiplies the index by the bytes in each block to get to the one we want, then adds 34 so we can get to the 

            
            
            

            short SegmentIndex = BitConverter.ToInt16(MapBytes, ResourceSegmentOffset); //Gets the segment index in tag resources and makes sure it's an int16

            if (debug == true)
            {
                Console.WriteLine("Segment Index: " + SegmentIndex); //Prints the segment index so we can know it has the right one
            }

            int PrimaryPageAddress = Segments + (SegmentIndex * 16); //The primary page index read from segments
            int SecondaryPageAddress = Segments + (SegmentIndex * 16) + 2; //Secondary page index

            short PrimaryPageIndex = BitConverter.ToInt16(MapBytes, PrimaryPageAddress); //Again, gets those ints and makes them int16
            short SecondaryPageIndex = BitConverter.ToInt16(MapBytes, SecondaryPageAddress); //These may be unncessary, but better safe than sorry

            if (debug == true)
            {
                Console.WriteLine("Primary Page Index: " + PrimaryPageIndex); //So we know we have the right index again
                Console.WriteLine("Secondary Page Index: " + SecondaryPageIndex);
            }

            int PrimaryRawPageAddress = (PrimaryPageIndex * 88) + RawPages; //The base address for the raw page we're looking for
            int SecondaryRawPageAddress = (SecondaryPageIndex * 88) + RawPages; //The same, but for the secondary raw page

            int PrimaryBlockOffset = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 8); //Like they say on the tin, all of these are reading from the Raw Pages block
            int SecondaryBlockOffset = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 8);
            int PrimaryCompressedBlockSize = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 12);
            int SecondaryCompressedBlockSize = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 12);
            int PrimaryUncompressedBlockSize = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 16);
            int SecondaryUncompressedBlockSize = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 16);
            int PrimaryCRCChecksum = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 20);
            int SecondaryCRCChecksum = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 20);

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
                //rebuild dds header here

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

            193ECC2C = segments = 423545900

            19367524 = raw pages = 422999332

            720 = battle rifle texture
            */

        }

        public static void ImportBitmap(string DDSName, string FileName)
        {

            Console.WriteLine("Enter the Tag Resources offset for the map. (found in the Zone tag)"); //These will eventually be replaced by something better
            int TagResource = GetInt();

            Console.WriteLine("Enter the Segments offset for the map (found in the Zone tag)"); //But until I can understand the galaxy brain stuff behind how Assembly reads maps
            int Segments = GetInt();

            Console.WriteLine("Enter the Raw Pages offset for the map (found in the Zone tag)"); //These are here to stay. Ideally the GetInt() method will eventually accept hex though.
            int RawPages = GetInt();

            Console.WriteLine("Enter the Asset Datum Index for the bitmap you want to import to"); //This one will probably stay until I can have it read tag names instead which will be quite a journey.
            int AssetIndex = GetInt();

            int ResourceSegmentOffset = (AssetIndex * 64) + TagResource + 34; //Multiplies the index by the bytes in each block to get to the one we want, then adds 34 so we can get to the 

            FileStream MapFS = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //The map read into a filestream
            byte[] MapBytes = new byte[MapFS.Length]; //Byte array with the same size as our map
            MapFS.Read(MapBytes, 0, MapBytes.Length); //Read the contents of our map into a byte array

            short SegmentIndex = BitConverter.ToInt16(MapBytes, ResourceSegmentOffset); //Gets the segment index in tag resources and makes sure it's an int16

            if (debug == true)
            {
                Console.WriteLine("Segment Index: " + SegmentIndex); //Prints the segment index so we can know it has the right one
            }

            int PrimaryPageAddress = Segments + (SegmentIndex * 16); //The primary page index read from segments
            int SecondaryPageAddress = Segments + (SegmentIndex * 16) + 2; //Secondary page index

            short PrimaryPageIndex = BitConverter.ToInt16(MapBytes, PrimaryPageAddress); //Again, gets those ints and makes them int16
            short SecondaryPageIndex = BitConverter.ToInt16(MapBytes, SecondaryPageAddress); //These may be unncessary, but better safe than sorry

            if (debug == true)
            {
                Console.WriteLine("Primary Page Index: " + PrimaryPageIndex); //So we know we have the right index again
                Console.WriteLine("Secondary Page Index: " + SecondaryPageIndex);
            }

            int PrimaryRawPageAddress = (PrimaryPageIndex * 88) + RawPages; //The base address for the raw page we're looking for
            int SecondaryRawPageAddress = (SecondaryPageIndex * 88) + RawPages; //The same, but for the secondary raw page

            int PrimaryBlockOffset = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 8); //Like they say on the tin, all of these are reading from the Raw Pages block
            int SecondaryBlockOffset = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 8);
            int PrimaryCompressedBlockSize = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 12);
            int SecondaryCompressedBlockSize = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 12);
            int PrimaryUncompressedBlockSize = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 16);
            int SecondaryUncompressedBlockSize = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 16);
            int PrimaryCRCChecksum = BitConverter.ToInt32(MapBytes, PrimaryRawPageAddress + 20);
            int SecondaryCRCChecksum = BitConverter.ToInt32(MapBytes, SecondaryRawPageAddress + 20);

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

            bool SecondaryPageSelected = false;
            bool PrimaryPageSelected = false;

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
                Console.WriteLine("Attempting to inject Secondary Page");
                MapFS.Seek(SecondaryBlockOffset, 0); //Head over to the offset where the dds data starts

                FileStream DDSStream = new FileStream(DDSName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //Reads the input DDS File into a stream
                byte[] DDSDecompressedBytes = new byte[SecondaryUncompressedBlockSize + 128]; //Empty byte array of the same size as the DDS file
                DDSStream.Read(DDSDecompressedBytes, 0, DDSDecompressedBytes.Length); //The byte array now contains the bytes from our file. We have to instead use DDSBytes.Length as DDSStream.Length returns a long instead of an Int32
                DDSStream.Close(); //We don't need the DDSStream anymore so we'll close it.
                DDSDecompressedBytes = DDSDecompressedBytes.Skip(128).ToArray();


                byte[] DDSCompressedBytes = new byte[SecondaryCompressedBlockSize]; //Byte array to hold the recompressed data

                using MemoryStream ms1 = new MemoryStream(DDSDecompressedBytes);
                {
                    using FileStream fs2 = new FileStream("TemporaryFile", FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    {
                        using DeflateStream ds1 = new DeflateStream(fs2, CompressionMode.Compress, true); //compress
                        {
                            ms1.CopyTo(ds1);
                        }
                    }
                }

                FileStream fs3 = new FileStream("TemporaryFile", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

                fs3.Read(DDSCompressedBytes, 0, SecondaryCompressedBlockSize);

                MapFS.Write(DDSCompressedBytes, 0, SecondaryCompressedBlockSize); //Writes our imported DDS into the file
                MapFS.Close();
                File.Delete("TemporaryFile");

                //0x1F9EF198 = tag resources 0x1F90B120 = segments 0x1F8635D0 = raw pages 725 = smg index
            }
            if (PrimaryPageSelected == true)
            {
                Console.WriteLine("Attempting to inject Primary Page");
                MapFS.Seek(PrimaryBlockOffset, 0); //Head over to the offset where the dds data starts

                FileStream DDSStream = new FileStream(DDSName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //Reads the input DDS File into a stream
                byte[] DDSDecompressedBytes = new byte[PrimaryUncompressedBlockSize + 128]; //Empty byte array of the same size as the DDS file
                DDSStream.Read(DDSDecompressedBytes, 0, DDSDecompressedBytes.Length); //The byte array now contains the bytes from our file. We have to instead use DDSBytes.Length as DDSStream.Length returns a long instead of an Int32
                DDSStream.Close(); //We don't need the DDSStream anymore so we'll close it.
                DDSDecompressedBytes = DDSDecompressedBytes.Skip(128).ToArray();


                byte[] DDSCompressedBytes = new byte[PrimaryCompressedBlockSize]; //Byte array to hold the recompressed data

                using MemoryStream ms1 = new MemoryStream(DDSDecompressedBytes);
                {
                    using FileStream fs2 = new FileStream("TemporaryFile", FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    {
                        using DeflateStream ds1 = new DeflateStream(fs2, CompressionMode.Compress, true); //compress
                        {
                            ms1.CopyTo(ds1);
                        }
                    }
                }

                FileStream fs3 = new FileStream("TemporaryFile", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

                fs3.Read(DDSCompressedBytes, 0, PrimaryCompressedBlockSize);

                MapFS.Write(DDSCompressedBytes, 0, PrimaryCompressedBlockSize); //Writes our imported DDS into the file
                MapFS.Close();
                File.Delete("TemporaryFile");
            }
        }



            public static int GetInt() //Attempts to get an int from the user and restarts the loop if it's 0 or not a number
        {
            int myInt = 0;
            bool assigned = false;
            while (assigned == false) //So it can keep trying rather than close the program
            {
                try
                {
                    char hex1 = '0'; //Silly workaround. Why can't you just use if (myString[0] == "0")?
                    char hex2 = 'x';
                    string myString = Console.ReadLine(); //Read a string from the user
                    if (myString[0] == hex1 && myString[1] == hex2) //If the string starts with 0x do this
                    {
                        string newString = myString.Remove(0, 2); //Remove the 0x so it can be parsed
                        myInt = int.Parse(newString, NumberStyles.HexNumber); //Translates the hex string into an int

                    }
                    else
                    {
                        myInt = Convert.ToInt32(myString); //If it doesn't start with 0x, we presume the user is using an int to begin with
                    }
                    
                }
                catch //If something goes wrong, do this
                {
                    
                    Console.WriteLine("Error: Please specify a numeric value above 0"); //Error so we know something is messing up rather than the neccesarily the user
                    continue;
                }
                if (myInt == 0)
                {
                    Console.WriteLine("Please specify a numeric value above 0");
                    continue;
                }
                if (myInt > 0) //If the int has been assigned, we can move on and not stay in this while loop
                {
                    assigned = true;
                }
            }
            return myInt; //Yay, you now have an int you can use at long last.
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
