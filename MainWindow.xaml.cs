using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Linq.Expressions;

namespace MCCBitmapIO //TODO: Truncate filenames even better, find some way of adding support for mipmaps (at least those in the main resource)
{ //Remember that 08 in the header needs to be 0A for mipmaps to show up
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public string mapName = "";
        public static int AssetIndex = 0;
        public static string IOFile = "";
        public static string mode = "none";
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

            long tag_group_table_address_real = BitConverter.ToInt64(tag_group_table_address, 0) + address_mask;

            long tag_table_address_real = BitConverter.ToInt64(tag_table_address, 0) + address_mask;

            //Tag group table
            MapFS.Seek(tag_group_table_address_real, 0);
            byte[] tag_group_magic = new byte[0x4];
            MapFS.Read(tag_group_magic, 0, 4);

            //Tag table
            MapFS.Seek(tag_table_address_real, 0); //int16
            byte[] tag_group_index = new byte[0x2];
            MapFS.Read(tag_group_index, 0, 2);

            int ZoneTagGroup = ZoneGroupTagLookUp(MapFS, tag_group_table_address_real, group_tag_count);

            long ZoneTagAddress = ZoneTagLookUp(MapFS, address_mask, tag_table_address_real, tag_count, ZoneTagGroup);

            //Console.WriteLine("Enter a Asset Datum Index for the bitmap you want to extract or import");
            //int AssetIndex = Convert.ToInt32(Console.ReadLine());

            ReadZone(MapFS, address_mask, ZoneTagAddress, tag_table_address_real, AssetIndex);
        }
        public static string ArrayToHex(byte[] array) //Converts the contents of an array to a hexadecimal string
        {
            uint myInt = BitConverter.ToUInt32(array, 0);
            return myInt.ToString("X");
        }
        public static string ArrayToString(byte[] array) //Converts the contents of an array to a numerical string
        {
            uint myInt = BitConverter.ToUInt32(array, 0);
            return myInt.ToString();
        }
        public static byte[] TagLookUp(FileStream MapFS, long tag_table_address_real, int tagIndex)
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
                    return memoryaddress;
                }
            }
            return new byte[0];
        }
        public static int ZoneGroupTagLookUp(FileStream MapFS, long tag_group_table_address_real, byte[] group_tag_count)
        {
            int iterations = 0;
            for (long i = tag_group_table_address_real; i < tag_group_table_address_real + (0x10 * BitConverter.ToInt32(group_tag_count, 0)); i += 0x10) //loops through each element in the group tag table looking for zone
            {
                byte[] enoz = { 0x65, 0x6E, 0x6F, 0x7A }; //zone but backwards because endian i guess
                MapFS.Seek(i, 0); //has to use i and i has to hhe the tag table address. dont think too much about it, it's just simpler this way
                byte[] group_magic = new byte[0x4]; //magic means tag type i guess
                MapFS.Read(group_magic, 0, 4); //read for whatever reason equates to "store this thing in a byte array"
                if (BitConverter.ToInt32(enoz, 0) == BitConverter.ToInt32(group_magic, 0)) //this really should work without the bitconversion but fuck it
                {
                    //Console.WriteLine("Zone is: {0}", iterations);
                    return iterations;
                }
                iterations++; //has to be at the end (after the return) or you end up getting the wrong number
            }
            return 0; //needs a return 0 or c# won't like it
        }
        public static long ZoneTagLookUp(FileStream MapFS, long address_mask, long tag_table_address_real, byte[] tag_count, int ZoneTagGroup)
        {
            for (long i = tag_table_address_real; i < tag_table_address_real + (0x8 * BitConverter.ToInt32(tag_count, 0)); i += 0x8) //loops through each element in the tag table looking for the group index
            {
                MapFS.Seek(i, 0); //has to use i and i has to be the the tag table address. dont think too much about it
                byte[] group_index = new byte[0x2]; //first thing in the tag table is the group index ie if it's a vehicle tag it'll be vehi
                MapFS.Read(group_index, 0, 2);
                int group_index_int = BitConverter.ToInt16(group_index, 0); //so we can compare it to our zone tag group we found from zonegrouptaglookup
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
        public static void ReadZone(FileStream MapFS, long address_mask, long tag_memory_address_real, long tag_table_address_real, int AssetIndex)
        {
            //Console.WriteLine(tag_memory_address_real);
            MapFS.Seek(tag_memory_address_real + 0x64, 0); //Go to tag offset plus the location of Tag Resources
            byte[] TagResourceCount = new byte[0x4];
            MapFS.Read(TagResourceCount, 0, 4); //Gets the number of entries in tag resources
                                                //int TagResourceCount_int = BitConverter.ToInt32(TagResourceCount);
            byte[] TagResourceAddress = new byte[0x4];
            MapFS.Read(TagResourceAddress, 0, 4); //Gets the virtual address for the block
            long TagResourceAddress_real = GetTagAddress(TagResourceAddress, address_mask);
            //Console.WriteLine(TagResourceAddress_real);

            MapFS.Seek(tag_memory_address_real + 0x58, 0); //Go to tag offset plus the location of Segments
            byte[] SegmentsCount = new byte[0x4];
            MapFS.Read(SegmentsCount, 0, 4); //Gets the number of entries in segments
                                             //int SegmentsCount_int = BitConverter.ToInt32(SegmentsCount);
            byte[] SegmentsAddress = new byte[0x4];
            MapFS.Read(SegmentsAddress, 0, 4); //Gets the virtual address for the block
            long SegmentsAddress_real = GetTagAddress(SegmentsAddress, address_mask);
            //Console.WriteLine(SegmentsAddress_real);

            MapFS.Seek(tag_memory_address_real + 0x34, 0); //Go to tag offset plus the location of raw pages
            byte[] RawPagesCount = new byte[0x4];
            MapFS.Read(RawPagesCount, 0, 4); //Gets the number of entries in raw pages
                                             //int RawPagesCount_int = BitConverter.ToInt32(RawPagesCount);
            byte[] RawPagesAddress = new byte[0x4];
            MapFS.Read(RawPagesAddress, 0, 4); //Gets the virtual address for the block
            long RawPagesAddress_real = GetTagAddress(RawPagesAddress, address_mask);
            //Console.WriteLine(RawPagesAddress_real);

            MapFS.Seek(TagResourceAddress_real + (0x40 * AssetIndex) + 0x0, 0);
            byte[] TagRefGroup = new byte[0x4];
            MapFS.Read(TagRefGroup, 0, 4);
            if (BitConverter.ToInt32(TagRefGroup, 0) != 1651078253)
            {
                MessageBox.Show("Datum Index refers to non-bitmap tag - are you sure you're using the right number?"); //Makes sure we are finding a bitmap and not some other tag
                MapFS.Close();
                return;
            }
            MapFS.Seek(TagResourceAddress_real + (0x40 * AssetIndex) + 0xC, 0); //Go to the datum index as there's some empty space in between the group magic
            byte[] Datum = new byte[0x4]; //Datums are confusing and weird.
            MapFS.Read(Datum, 0, 4);
            MapFS.Seek(TagResourceAddress_real + (0x40 * AssetIndex) + 0x22, 0); //The segment index. Last thing we need from this block now.
            byte[] SegmentIndex_byte = new byte[0x2];
            MapFS.Read(SegmentIndex_byte, 0, 2);
            int SegmentsIndex = BitConverter.ToInt16(SegmentIndex_byte, 0); //Onwards
                                                                         //Console.WriteLine("Segments Index: " + SegmentsIndex);

            MapFS.Seek(SegmentsAddress_real + (SegmentsIndex * 0x10), 0); //Choo choo, we're at going to the segments block
                                                                          //Console.WriteLine(SegmentsAddress_real);
            byte[] PrimaryPageIndex = new byte[0x2]; //Now what to do with these? 
            MapFS.Read(PrimaryPageIndex, 0, 2);
            byte[] SecondaryPageIndex = new byte[0x2]; 
            MapFS.Read(SecondaryPageIndex, 0, 2);
            int PrimaryPageInt = BitConverter.ToInt16(PrimaryPageIndex, 0);
            int SecondaryPageInt = BitConverter.ToInt16(SecondaryPageIndex, 0);

            bool TwoPages = true;

            //We check here what page there is to be used. If there's a secondary page we'll extract and use that over the primary, otherwise we just use the primary.
            if (BitConverter.ToInt16(SecondaryPageIndex, 0) == 0 && BitConverter.ToInt16(PrimaryPageIndex, 0) != 0)
            {
                TwoPages = false;
            }
            if (BitConverter.ToInt16(SecondaryPageIndex, 0) == 0 && BitConverter.ToInt16(PrimaryPageIndex, 0) == 0)
            {
                MessageBox.Show("Something went wrong, no raw pages could be found"); //In case of weirdness
                MapFS.Close();
                return;
            }
            else if (BitConverter.ToInt16(SecondaryPageIndex, 0) != 0)
            {
                TwoPages = true;
            }
            /*if (PageIndex == 0)
            {
                MessageBox.Show("Something went wrong, page couldn't be chosen"); //In case of even more weirdness
                MapFS.Close();
                return;
            }*/

            //Console.WriteLine("Page Index: " + PageIndex);
            MapFS.Seek(RawPagesAddress_real + (SecondaryPageInt * 0x58) + 0x8, 0); //Get Secondary page first if it exists

            byte[] SecondaryBlockOffset_byte = new byte[0x4]; //Place we go to for doing stuff with the bitmap
            MapFS.Read(SecondaryBlockOffset_byte, 0, 4);
            byte[] SecondaryCompressedSize_byte = new byte[0x4]; //The size it should be in the file
            MapFS.Read(SecondaryCompressedSize_byte, 0, 4);
            byte[] SecondaryUncompressedSize_byte = new byte[0x4]; //The size it should be when decompressed
            MapFS.Read(SecondaryUncompressedSize_byte, 0, 4);

            int SecondaryBlockOffset = BitConverter.ToInt32(SecondaryBlockOffset_byte, 0);
            int SecondaryCompressedSize = BitConverter.ToInt32(SecondaryCompressedSize_byte, 0);
            int SecondaryUncompressedSize = BitConverter.ToInt32(SecondaryUncompressedSize_byte, 0);

            MapFS.Seek(RawPagesAddress_real + (PrimaryPageInt * 0x58) + 0x8, 0); //Get Primary Page afterwards 

            byte[] PrimaryBlockOffset_byte = new byte[0x4]; //Place we go to for doing stuff with the bitmap
            MapFS.Read(PrimaryBlockOffset_byte, 0, 4);
            byte[] PrimaryCompressedSize_byte = new byte[0x4]; //The size it should be in the file
            MapFS.Read(PrimaryCompressedSize_byte, 0, 4);
            byte[] PrimaryUncompressedSize_byte = new byte[0x4]; //The size it should be when decompressed
            MapFS.Read(PrimaryUncompressedSize_byte, 0, 4);

            int PrimaryBlockOffset = BitConverter.ToInt32(PrimaryBlockOffset_byte, 0);
            int PrimaryCompressedSize = BitConverter.ToInt32(PrimaryCompressedSize_byte, 0);
            int PrimaryUncompressedSize = BitConverter.ToInt32(PrimaryUncompressedSize_byte, 0);



            //Moving on for a while, we need to go grab the bitmap info for remaking the DDS header. 

            int DatumIndex = BitConverter.ToInt32(Datum, 0) & 0xFFFF; //Tag index. Don't ask. I don't know.
            byte[] TagMemoryAddress = TagLookUp(MapFS, tag_table_address_real, DatumIndex); //Gets the memory address for the referenced tag
            long TagMemoryAddress_real = GetTagAddress(TagMemoryAddress, address_mask);
            //Now we have all we need from Zone for now

            MapFS.Seek(TagMemoryAddress_real + 0x60, 0); //Now we go to our tag from zone, then to it's tagblock for bitmaps so we can get the juicy information inside
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
            byte[] BitmapFormatArray = new byte[0x2];
            MapFS.Read(BitmapFormatArray, 0, 2); //this is an enum. 16 is DXT5 which is the ordinary but could be others.
            byte[] BitmapFlagsArray = new byte[0x2];
            MapFS.Read(BitmapFlagsArray, 0, 2); //Oh boy. This is gonna be fun to figure out. Like an enum but with multiple choice. (Actually not that bad, it's just addition)
            MapFS.Seek(BitmapsAddress_real + 0x14, 0);
            byte[] MipMapCountArray = new byte[0x1];
            MapFS.Read(MipMapCountArray, 0, 1);


            //int BitmapWidth = BitConverter.ToInt32(BitmapWidthArray);
            //int BitmapHeight = BitConverter.ToInt32(BitmapHeightArray, 0);
            //int BitmapDepth = BitConverter.ToInt32(BitmapDepthArray, 0); 
            int BitmapFormat = BitConverter.ToInt16(BitmapFormatArray, 0);
            //int MipMapCount = BitConverter.ToInt32(MipMapCountArray, 0);
            //int BitmapFlags = BitConverter.ToInt32(BitmapFlagsArray , 0); //unused

            //Now that we're done here, let's get to ripping out the headerless file

            DDSHeader H3Header = new DDSHeader();


            switch (BitmapFormat)
            {
                case 11: //A8R8G8B8
                    H3Header.Format[0] = 0x0;
                    H3Header.Format[1] = 0x0;
                    H3Header.Format[2] = 0x0;
                    H3Header.Format[3] = 0x0;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x41;
                    H3Header.PixelFormat[0xC] = 0x20;
                    H3Header.PixelFormat[0x12] = 0xFF;
                    H3Header.PixelFormat[0x15] = 0xFF;
                    H3Header.PixelFormat[0x18] = 0xFF;
                    H3Header.PixelFormat[0x1F] = 0xFF;
                    break;
                case 14: //DXT1
                    H3Header.Format[0] = 0x44;
                    H3Header.Format[1] = 0x58;
                    H3Header.Format[2] = 0x54;
                    H3Header.Format[3] = 0x31;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    break;
                case 15: //DXT3
                    H3Header.Format[0] = 0x44;
                    H3Header.Format[1] = 0x58;
                    H3Header.Format[2] = 0x54;
                    H3Header.Format[3] = 0x33;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    break;
                case 16: //DXT5
                    H3Header.Format[0] = 0x44;
                    H3Header.Format[1] = 0x58;
                    H3Header.Format[2] = 0x54;
                    H3Header.Format[3] = 0x35;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    break;
                case 33: //DXN (ATI2)
                    H3Header.Format[0] = 0x41;
                    H3Header.Format[1] = 0x54;
                    H3Header.Format[2] = 0x49;
                    H3Header.Format[3] = 0x32;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    break;
                /*case 39: //DXN_mono_alpha, no idea
                    H3Header.Format[0] = 0x41;
                    H3Header.Format[1] = 0x54;
                    H3Header.Format[2] = 0x49;
                    H3Header.Format[3] = 0x32;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    break; */
                default: //In case it's something else, just use DXT5, fuck it
                    H3Header.Format[0] = 0x44;
                    H3Header.Format[1] = 0x58;
                    H3Header.Format[2] = 0x54;
                    H3Header.Format[3] = 0x35;
                    H3Header.PixelFormat[0] = 0x20;
                    H3Header.PixelFormat[4] = 0x04;
                    MessageBox.Show("DDS Format was unsupported, defaulting to DXT5 - you may have issues loading this texture into an editor");
                    break;
                
            }


            if (mode.ToLower() == "extract" || mode.ToLower() == "e")
            {
                if (TwoPages == false)
                {
                    MapFS.Seek(PrimaryBlockOffset, 0); //Head over to the offset where our raw dds data starts
                    byte[] PrimaryCompressedPageData = new byte[PrimaryCompressedSize]; //A byte array allocated to the size of the compressed data
                    MapFS.Read(PrimaryCompressedPageData, 0, PrimaryCompressedSize); //Reads from the stream into the previous byte array, but only the amount of bytes we need

                    byte[] DecompressedPageData = new byte[PrimaryUncompressedSize + 128]; //A byte array created with the size of the decompressed data plus the DDS header


                    using MemoryStream SecondaryMemStream = new MemoryStream(PrimaryCompressedPageData); //Create a new memory stream with the compressed data
                    {
                        using DeflateStream DFSecondary = new DeflateStream(SecondaryMemStream, CompressionMode.Decompress, true); //Read the memory stream into a deflatestream to decompress it
                        {

                            DFSecondary.Read(DecompressedPageData, 128, PrimaryUncompressedSize); //Read the decompressed data into a byte array
                        }
                    }
                    //Header stuff, yay
                    for (int b = 0; b < 129; b++) //Loops through the length of the header setting each thing as it needs. For now we're just going to do the bare minimum.
                    {
                        switch (b)
                        {
                            case 0:
                                DecompressedPageData[b] = 0x44;
                                break;
                            case 1:
                                DecompressedPageData[b] = 0x44;
                                break;
                            case 2:
                                DecompressedPageData[b] = 0x53;
                                break;
                            case 3:
                                DecompressedPageData[b] = 0x20;
                                break;
                            case 4:
                                DecompressedPageData[b] = 0x7C;
                                break;
                            case 8:
                                DecompressedPageData[b] = 0x07;
                                break;
                            case 9:
                                DecompressedPageData[b] = 0x10;
                                break;
                            case 0x0A:
                                DecompressedPageData[b] = 0x08;
                                break;
                            case 0xC:
                                DecompressedPageData[b] = BitmapHeightArray[0];
                                break;
                            case 0x0D:
                                DecompressedPageData[b] = BitmapHeightArray[1];
                                break;
                            case 0x10:
                                DecompressedPageData[b] = BitmapWidthArray[0];
                                break;
                            case 0x11:
                                DecompressedPageData[b] = BitmapWidthArray[1];
                                break;
                            case 0x4C:
                                DecompressedPageData[b] = H3Header.PixelFormat[0];
                                break;
                            case 0x50:
                                DecompressedPageData[b] = H3Header.PixelFormat[4];
                                break;
                            case 0x54:
                                DecompressedPageData[b] = H3Header.Format[0];
                                break;
                            case 0x55:
                                DecompressedPageData[b] = H3Header.Format[1];
                                break;
                            case 0x56:
                                DecompressedPageData[b] = H3Header.Format[2];
                                break;
                            case 0x57:
                                DecompressedPageData[b] = H3Header.Format[3];
                                break;
                            case 0x58:
                                DecompressedPageData[b] = H3Header.PixelFormat[0xC];
                                break;
                            case 0x5E:
                                DecompressedPageData[b] = H3Header.PixelFormat[0x12];
                                break;
                            case 0x61:
                                DecompressedPageData[b] = H3Header.PixelFormat[0x15];
                                break;
                            case 0x64:
                                DecompressedPageData[b] = H3Header.PixelFormat[0x18];
                                break;
                            case 0x6B:
                                DecompressedPageData[b] = H3Header.PixelFormat[0x1F];
                                break;
                        }
                    }

                    File.WriteAllBytes(IOFile, DecompressedPageData);
                    string completiontext = "Done.";
                    MessageBox.Show(completiontext);
                    MapFS.Close();
                }
                if (TwoPages == true)
                {
                    MapFS.Seek(SecondaryBlockOffset, 0); //Head over to the offset where our raw dds data starts
                    byte[] SecondaryCompressedPageData = new byte[SecondaryCompressedSize]; //A byte array allocated to the size of the compressed data
                    MapFS.Read(SecondaryCompressedPageData, 0, SecondaryCompressedSize); //Reads from the stream into the previous byte array, but only the amount of bytes we need

                    MapFS.Seek(PrimaryBlockOffset, 0); //Head over to the offset where our raw dds data starts
                    byte[] PrimaryCompressedPageData = new byte[PrimaryCompressedSize]; //A byte array allocated to the size of the compressed data
                    MapFS.Read(PrimaryCompressedPageData, 0, PrimaryCompressedSize); //Reads from the stream into the previous byte array, but only the amount of bytes we need


                    byte[] SecondaryDecompressedPageData = new byte[SecondaryUncompressedSize + 128]; //A byte array created with the size of the decompressed data plus the DDS header
                    byte[] PrimaryDecompressedPageData = new byte[PrimaryUncompressedSize]; //A byte array created with the size of the decompressed data


                    using MemoryStream SecondaryMemStream = new MemoryStream(SecondaryCompressedPageData); //Create a new memory stream with the compressed data
                    {
                        using DeflateStream DFSecondary = new DeflateStream(SecondaryMemStream, CompressionMode.Decompress, true); //Read the memory stream into a deflatestream to decompress it
                        {

                            DFSecondary.Read(SecondaryDecompressedPageData, 128, SecondaryUncompressedSize); //Read the decompressed data into a byte array
                        }
                    }

                    using MemoryStream PrimaryMemStream = new MemoryStream(PrimaryCompressedPageData); //Create a new memory stream with the compressed data
                    {
                        using DeflateStream DFPrimary = new DeflateStream(PrimaryMemStream, CompressionMode.Decompress, true); //Read the memory stream into a deflatestream to decompress it
                        {

                            DFPrimary.Read(PrimaryDecompressedPageData, 0, PrimaryUncompressedSize); //Read the decompressed data into a byte array
                        }
                    }
                    //Header stuff, yay
                    for (int b = 0; b < 129; b++) //Loops through the length of the header setting each thing as it needs. For now we're just going to do the bare minimum.
                    {
                        switch (b)
                        {
                            case 0:
                                SecondaryDecompressedPageData[b] = 0x44;
                                break;
                            case 1:
                                SecondaryDecompressedPageData[b] = 0x44;
                                break;
                            case 2:
                                SecondaryDecompressedPageData[b] = 0x53;
                                break;
                            case 3:
                                SecondaryDecompressedPageData[b] = 0x20;
                                break;
                            case 4:
                                SecondaryDecompressedPageData[b] = 0x7C;
                                break;
                            case 8:
                                SecondaryDecompressedPageData[b] = 0x07;
                                break;
                            case 9:
                                SecondaryDecompressedPageData[b] = 0x10;
                                break;
                            case 0x0A:
                                SecondaryDecompressedPageData[b] = 0x08;
                                break;
                            case 0xC:
                                SecondaryDecompressedPageData[b] = BitmapHeightArray[0];
                                break;
                            case 0x0D:
                                SecondaryDecompressedPageData[b] = BitmapHeightArray[1];
                                break;
                            case 0x10:
                                SecondaryDecompressedPageData[b] = BitmapWidthArray[0];
                                break;
                            case 0x11:
                                SecondaryDecompressedPageData[b] = BitmapWidthArray[1];
                                break;
                            case 0x4C:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0];
                                break;
                            case 0x50:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[4];
                                break;
                            case 0x54:
                                SecondaryDecompressedPageData[b] = H3Header.Format[0];
                                break;
                            case 0x55:
                                SecondaryDecompressedPageData[b] = H3Header.Format[1];
                                break;
                            case 0x56:
                                SecondaryDecompressedPageData[b] = H3Header.Format[2];
                                break;
                            case 0x57:
                                SecondaryDecompressedPageData[b] = H3Header.Format[3];
                                break;
                            case 0x58:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0xC];
                                break;
                            case 0x5E:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0x12];
                                break;
                            case 0x61:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0x15];
                                break;
                            case 0x64:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0x18];
                                break;
                            case 0x6B:
                                SecondaryDecompressedPageData[b] = H3Header.PixelFormat[0x1F];
                                break;
                        }
                    }

                    byte[] CombinedDecompressedPageData = SecondaryDecompressedPageData.Concat(PrimaryDecompressedPageData).ToArray();

                    File.WriteAllBytes(IOFile, CombinedDecompressedPageData);
                    string completiontext = "Done.";
                    MessageBox.Show(completiontext);
                    MapFS.Close();
                }
            }
            if (mode.ToLower() == "import" || mode.ToLower() == "i")
            {
                if (TwoPages == false)
                {
                    Console.WriteLine("Please type the name of the file you want to import");
                    //string IOFile = Convert.ToString(Console.ReadLine());
                    MapFS.Seek(PrimaryBlockOffset, 0); //Head over to the offset where our raw dds data starts
                    FileStream DDSStream = new FileStream(IOFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //Reads the input DDS File into a stream
                    byte[] DDSDecompressedBytes = new byte[DDSStream.Length]; //Empty byte array of the same size as the DDS file
                    DDSStream.Read(DDSDecompressedBytes, 0, DDSDecompressedBytes.Length); //The byte array now contains the bytes from our file. We have to instead use DDSBytes.Length as DDSStream.Length returns a long instead of an Int32
                    DDSStream.Close(); //We don't need the DDSStream anymore so we'll close it.
                    DDSDecompressedBytes = DDSDecompressedBytes.Skip(128).ToArray();




                    MemoryStream DecompressedStream = new MemoryStream(DDSDecompressedBytes);

                    MemoryStream CompressedStream = new MemoryStream();

                    using (DeflateStream df1 = new DeflateStream(CompressedStream, CompressionMode.Compress, true))
                    {
                        DecompressedStream.CopyTo(df1);
                    }
                    byte[] DDSCompressedBytes = new byte[CompressedStream.Length]; //Byte array to hold the recompressed data
                    int CompressedBytesLength = DDSCompressedBytes.Length;
                    int DecompressedBytesLength = DDSDecompressedBytes.Length;
                    CompressedStream.Seek(0, 0); //WHY DOES IT GET SET TO THE END? THAT MAKES NO SENSE!
                    CompressedStream.Read(DDSCompressedBytes, 0, CompressedBytesLength);

                    if (CompressedBytesLength > PrimaryCompressedSize)
                    {
                        MessageBox.Show("File size too large");
                        DDSStream.Close();
                        MapFS.Close();
                        return;
                    }
                    if (DecompressedBytesLength > PrimaryUncompressedSize)
                    {
                        MessageBox.Show("File size too large");
                        DDSStream.Close();
                        MapFS.Close();
                        return;
                    }

                    MapFS.Write(DDSCompressedBytes, 0, CompressedBytesLength); //Writes our imported DDS into the file

                    byte[] DecompressedLengthBytes = BitConverter.GetBytes(DecompressedBytesLength);
                    byte[] CompressedLengthBytes = BitConverter.GetBytes(CompressedBytesLength);
                    byte[] CombinedBytes = CompressedLengthBytes.Concat(DecompressedLengthBytes).ToArray();

                    /*MapFS.Seek(RawPagesAddress_real + (PrimaryPageInt * 0x58) + 0xC, 0);
                    MapFS.Write(CombinedBytes, 0, 8); //Fuck this shit, why does it not let me write two things? Makes no goddamn sense
                    MapFS.Flush();*/
                    string completiontext = "Done.";
                    MessageBox.Show(completiontext);
                    MapFS.Close();
                }
                if (TwoPages == true)
                {
                    Console.WriteLine("Please type the name of the file you want to import");
                    //string IOFile = Convert.ToString(Console.ReadLine());
                    FileStream DDSStream = new FileStream(IOFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite); //Reads the input DDS File into a stream
                    if (DDSStream.Length > PrimaryUncompressedSize + SecondaryUncompressedSize + 128)
                    {
                        MessageBox.Show("File too large - ensure the file is the same size as the original.");
                        DDSStream.Close();
                        MapFS.Close();
                        return;
                    }
                    byte[] SecondaryDecompressedBytes = new byte[SecondaryUncompressedSize+128];
                    byte[] PrimaryDecompressedBytes = new byte[PrimaryUncompressedSize];
                    //byte[] DDSDecompressedBytes = new byte[DDSStream.Length]; //Empty byte array of the same size as the DDS file
                    DDSStream.Read(SecondaryDecompressedBytes, 0, SecondaryUncompressedSize+128); //Big assumption that the files are the same size, this will read all the secondary (not mipmaps) into a byte[]
                    SecondaryDecompressedBytes = SecondaryDecompressedBytes.Skip(128).ToArray();
                    DDSStream.Read(PrimaryDecompressedBytes, 0, PrimaryUncompressedSize); //Now the same for the mipmaps. Oh boy, how could this ever go wrong?
                    DDSStream.Close(); //We don't need the DDSStream anymore so we'll close it.

                    MemoryStream SecondaryDecompressedStream = new MemoryStream(SecondaryDecompressedBytes);

                    MemoryStream SecondaryCompressedStream = new MemoryStream();

                    using (DeflateStream SecondaryDS = new DeflateStream(SecondaryCompressedStream, CompressionMode.Compress, true))
                    {
                        SecondaryDecompressedStream.CopyTo(SecondaryDS);
                    }
                    byte[] SecondaryCompressedBytes = new byte[SecondaryCompressedStream.Length]; //Byte array to hold the recompressed data
                    int SecondaryCompressedBytesLength = SecondaryCompressedBytes.Length;
                    int SecondaryDecompressedBytesLength = SecondaryDecompressedBytes.Length;
                    SecondaryCompressedStream.Seek(0, 0); //WHY DOES IT GET SET TO THE END? THAT MAKES NO SENSE!
                    SecondaryCompressedStream.Read(SecondaryCompressedBytes, 0, SecondaryCompressedBytesLength);

                    MemoryStream PrimaryDecompressedStream = new MemoryStream(PrimaryDecompressedBytes);

                    MemoryStream PrimaryCompressedStream = new MemoryStream();

                    using (DeflateStream PrimaryDS = new DeflateStream(PrimaryCompressedStream, CompressionMode.Compress, true))
                    {
                        PrimaryDecompressedStream.CopyTo(PrimaryDS);
                    }
                    byte[] PrimaryCompressedBytes = new byte[PrimaryCompressedStream.Length]; //Byte array to hold the recompressed data
                    int PrimaryCompressedBytesLength = PrimaryCompressedBytes.Length;
                    int PrimaryDecompressedBytesLength = PrimaryDecompressedBytes.Length;
                    PrimaryCompressedStream.Seek(0, 0); //WHY DOES IT GET SET TO THE END? THAT MAKES NO SENSE!
                    PrimaryCompressedStream.Read(PrimaryCompressedBytes, 0, PrimaryCompressedBytesLength);

                    /*if (CompressedBytesLength > PrimaryCompressedSize + SecondaryCompressedSize)
                    {
                        MessageBox.Show("File size too large");
                        MapFS.Close();
                        return;
                    }
                    if (DecompressedBytesLength > PrimaryUncompressedSize + SecondaryUncompressedSize)
                    {
                        MessageBox.Show("File size too large");
                        MapFS.Close();
                        return;
                    }*/

                    MapFS.Seek(SecondaryBlockOffset, 0); //Head over to the offset where our secondary raw dds data starts
                    MapFS.Write(SecondaryCompressedBytes, 0, SecondaryCompressedBytesLength); //Writes our imported DDS into the file
                    MapFS.Flush();

                    MapFS.Seek(PrimaryBlockOffset, 0);
                    MapFS.Write(PrimaryCompressedBytes, 0, PrimaryCompressedBytesLength);
                    MapFS.Flush();

                    /*
                    byte[] SecondaryDecompressedLengthBytes = BitConverter.GetBytes(SecondaryDecompressedBytesLength); 
                    byte[] SecondaryCompressedLengthBytes = BitConverter.GetBytes(SecondaryCompressedBytesLength);
                    byte[] SecondaryCombinedBytes = SecondaryCompressedLengthBytes.Concat(SecondaryDecompressedLengthBytes).ToArray();

                    MapFS.Seek(RawPagesAddress_real + (SecondaryPageInt * 0x58) + 0xC, 0);
                    MapFS.Write(SecondaryCombinedBytes, 0, 8); //Fuck this shit, why does it not let me write two things? Makes no goddamn sense
                    MapFS.Flush();

                    byte[] PrimaryDecompressedLengthBytes = BitConverter.GetBytes(PrimaryDecompressedBytesLength);
                    byte[] PrimaryCompressedLengthBytes = BitConverter.GetBytes(PrimaryCompressedBytesLength);
                    byte[] PrimaryCombinedBytes = PrimaryCompressedLengthBytes.Concat(PrimaryDecompressedLengthBytes).ToArray();

                    MapFS.Seek(RawPagesAddress_real + (PrimaryPageInt * 0x58) + 0xC, 0);
                    MapFS.Write(PrimaryCombinedBytes, 0, 8);
                    MapFS.Flush();
                    */

                    string completiontext = "Done.";
                    MessageBox.Show(completiontext);
                    MapFS.Close();
                }
            }
            else if (mode != "e" && mode != "extract" && mode != "i" && mode != "import")
            {
                MessageBox.Show("Neither extract or import was selected");
                MapFS.Close();
                return;
            }
        }
        public static long GetTagAddress(byte[] tag_memory_address, long address_mask) //specifically for tags and tagblocks because of the int32 and * 4 requirement
        {
            long tag_memory_address_int = BitConverter.ToInt32(tag_memory_address, 0); //have to multiply the virtual address by 4. don't know why, just do it
            long tag_memory_address_4x = tag_memory_address_int * 4; //but you can't do it on the same line. again, dunno why.
            long tag_memory_address_real = tag_memory_address_4x + address_mask; //now you can add the address mask
            return tag_memory_address_real; //now this is the real address.
        }

        private void MapButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openMap = new OpenFileDialog();
            bool? result = openMap.ShowDialog(); //think ? just means it can be either true, false or nothing. nullables need more investigamation
            if (result == true)
                mapName = openMap.FileName;
            if (mapName.Length <= 199)
                MapTextBox.Text = mapName;
            else if (mapName.Length > 199)
                MapTextBox.Text = mapName;//.Substring(28);
        }

        private void IOButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openIO = new OpenFileDialog();
            SaveFileDialog saveIO = new SaveFileDialog();
            
            if (mode == "import")
            {
                bool? result = openIO.ShowDialog(); //think ? just means it can be either true, false or nothing. nullables need more investigamation
                if (result == true)
                {
                    IOFile = openIO.FileName;
                    if (IOFile.Length <= 99)
                        IOTextBox.Text = IOFile;
                    else if (IOFile.Length > 99)
                        IOTextBox.Text = IOFile;//.Substring(28);
                    
                }
            }
            if (mode == "extract")
            {
                bool? result = saveIO.ShowDialog();
                if (result == true)
                {
                    IOFile = saveIO.FileName;
                    if (IOFile.Length <= 99)
                        IOTextBox.Text = IOFile;
                    else if (IOFile.Length > 99)
                        IOTextBox.Text = IOFile;//.Substring(28);
                }
            }
        }

        private void ExtractButton_Checked(object sender, RoutedEventArgs e)
        {
            ImportButton.IsChecked = false;
            mode = "extract";
        }

        private void ImportButton_Checked(object sender, RoutedEventArgs e)
        {
            ExtractButton.IsChecked = false;
            mode = "import";
        }

        private void IOTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            IOFile = IOTextBox.Text;
        }

        private void IndexBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                AssetIndex = Convert.ToInt32(IndexBox.Text);
            }
            catch
            {
                IndexBox.Text = "0";
            }
        }
        private void BeginButton_Click(object sender, RoutedEventArgs e)
        {   
                if (AssetIndex > 0 && mapName.Length > 0 && IOFile.Length > 0)
                {
                    ReadMap(mapName);
                }
        }
    }

    class DDSHeader
    {
        public byte[] Format = new byte[4];
        public byte[] PixelFormat = new byte[0x20];
    }
}

