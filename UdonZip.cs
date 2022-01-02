/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2020 Foorack
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

/*
 *     UdonZip
 *
 *     Version log:
 *         0.1.0: 2020-05-30; Initial version.
 * 
 */

#define NO_DEBUG
// Remove NO_ to enable debug

using System;
using UnityEngine;
using UdonSharp;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBeMadeStatic.Global
// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
// ReSharper disable MemberCanBeMadeStatic.Local
public class UdonZip : UdonSharpBehaviour
{
    private const int INFLATE_DATA_SOURCE = 0;
    private const int INFLATE_DATA_SOURCE_INDEX = 1;
    private const int INFLATE_DATA_DEST = 2;
    private const int INFLATE_DATA_DEST_LENGTH = 3;
    private const int INFLATE_DATA_TAG = 4;
    private const int INFLATE_DATA_BITCOUNT = 5;

    private const int INFLATE_DATA_LTREE = 6;
    private const int INFLATE_DATA_DTREE = 7;

    private const int INFLATE_TREE_TABLE = 0;
    private const int INFLATE_TREE_TRANS = 1;

    private const int COMPRESSION_METHOD_NONE = 0;
    private const int COMPRESSION_METHOD_INFLATE = 8;

    private const int EOCD_TOTAL_CDS = 0;
    private const int EOCD_SIZE_OF_CD = 1;
    private const int EOCD_CD_OFFSET = 2;
    private const int EOCD_COMMENT = 3;

    private const int CD_VERSION = 0;
    private const int CD_MIN_VERSION = 1;
    private const int CD_BITFLAG = 2;
    private const int CD_COMPRESSION_METHOD = 3;
    private const int CD_LAST_MODIFICATION_TIME = 4;
    private const int CD_LAST_MODIFICATION_DATE = 5;
    private const int CD_CRC_32 = 6;
    private const int CD_COMPRESSED_SIZE = 7;
    private const int CD_UNCOMPRESSED_SIZE = 8;
    private const int CD_NAME_LENGTH = 9;
    private const int CD_EXTRA_FIELD_LENGTH = 10;
    private const int CD_COMMENT_LENGTH = 11;
    private const int CD_INTERNAL_FILE_ATTR = 12;
    private const int CD_EXTERNAL_FILE_ATTR = 13;
    private const int CD_OFFSET_LFH = 14;
    private const int CD_NAME = 15;
    private const int CD_EXTRA_FIELD = 16;
    private const int CD_COMMENT = 17;
    private const int CD_START_OF_NEXT_CD = 18;

    private const int LFH_MIN_VERSION = 0;
    private const int LFH_BITFLAG = 1;
    private const int LFH_COMPRESSION_METHOD = 2;
    private const int LFH_LAST_MODIFICATION_TIME = 3;
    private const int LFH_LAST_MODIFICATION_DATE = 4;
    private const int LFH_CRC_32 = 5;
    private const int LFH_COMPRESSED_SIZE = 6;
    private const int LFH_UNCOMPRESSED_SIZE = 7;
    private const int LFH_NAME_LENGTH = 8;
    private const int LFH_EXTRA_FIELD_LENGTH = 9;
    private const int LFH_NAME = 10;
    private const int LFH_EXTRA_FIELD = 11;
    private const int LFH_START_OF_DATA = 12;

    private const int ARCHIVE_EOCD = 0;
    private const int ARCHIVE_ENTIRES = 1;

    private const int FILEENTRY_CD = 0;
    private const int FILEENTRY_UNCOMPRESSED = 1;
    private const int FILEENTRY_COMPRESSED = 2;

    private bool hasBeenInit;

    #region INFLATE

    /***********************
     * INFLATE SECTION CODE
     ***********************/

    /*
     * {
     *     0: table (ushort[16])
     *     1: trans (ushort[288])
     * }
     */
    private object[] _sltree, _sdtree;

    /* extra bits and base tables for length codes */
    private readonly byte[] length_bits = new byte[30];
    private readonly ushort[] length_base = new ushort[30];

    /* extra bits and base tables for distance codes */
    private readonly byte[] dist_bits = new byte[30];
    private readonly ushort[] dist_base = new ushort[30];


    /* special ordering of code length codes */
    private readonly byte[] clcidx = new byte[]
    {
        16, 17, 18, 0, 8, 7, 9, 6,
        10, 5, 11, 4, 12, 3, 13, 2,
        14, 1, 15
    };

    /* used by tinf_decode_trees, avoids allocations every call */
    private readonly object[] code_tree = new object[] {new ushort[16], new ushort[288]};
    private readonly byte[] lengths = new byte[288 + 32];

    private object[] NewEmptyTree()
    {
        return new object[] {new ushort[16], new ushort[288]};
    }

    /* build the fixed huffman trees */
    private void tinf_build_fixed_trees(object[] lt, object[] dt)
    {
        int i;

        /* build fixed length tree */
        for (i = 0; i < 7; ++i)
        {
            // lt.table[i] = 0;
            ((ushort[]) lt[INFLATE_TREE_TABLE])[i] = 0;
        }

        ((ushort[]) lt[INFLATE_TREE_TABLE])[7] = 24;
        ((ushort[]) lt[INFLATE_TREE_TABLE])[8] = 152;
        ((ushort[]) lt[INFLATE_TREE_TABLE])[9] = 112;

        for (i = 0; i < 24; ++i)
        {
            // lt.trans[i] = 256 + i;
            ((ushort[]) lt[INFLATE_TREE_TRANS])[i] = (ushort) (256 + i);
        }

        for (i = 0; i < 144; ++i)
        {
            // lt.trans[24 + i] = i;
            ((ushort[]) lt[INFLATE_TREE_TRANS])[i] = (ushort) i;
        }

        for (i = 0; i < 8; ++i)
        {
            // lt.trans[24 + 144 + i] = 280 + i;
            ((ushort[]) lt[INFLATE_TREE_TRANS])[24 + 144 + i] = (ushort) (280 + i);
        }

        for (i = 0; i < 112; ++i)
        {
            // lt.trans[24 + 144 + 8 + i] = 144 + i;
            ((ushort[]) lt[INFLATE_TREE_TRANS])[24 + 144 + 8 + i] = (ushort) (144 + i);
        }

        /* build fixed distance tree */
        for (i = 0; i < 5; ++i)
        {
            ((ushort[]) dt[INFLATE_TREE_TABLE])[i] = 0;
        }

        // dt.table[5] = 32;
        ((ushort[]) dt[INFLATE_TREE_TABLE])[5] = 32;

        for (i = 0; i < 32; ++i)
        {
            // dt.trans[i] = i;
            ((ushort[]) dt[INFLATE_TREE_TRANS])[i] = 0;
        }
    }

    // ReSharper disable once ParameterHidesMember
    private void INFLATEBuildTree(object[] t, byte[] lengths, int off, int num)
    {
        var offs = new ushort[16];

        /* clear code length count table */
        for (ushort i = 0; i < 16; i += 1)
        {
            ((ushort[]) t[INFLATE_TREE_TABLE])[i] = 0;
        }

        /* scan symbol lengths, and sum code length counts */
        for (ushort i = 0; i < num; i += 1)
        {
            // t.table[lengths[off + i]]++;
            ((ushort[]) t[INFLATE_TREE_TABLE])[lengths[off + i]] += 1;
        }

        // t.table[0] = 0;
        ((ushort[]) t[INFLATE_TREE_TABLE])[0] = 0;

        /* compute offset table for distribution sort */
        for (ushort sum = 0, i = 0; i < 16; i += 1)
        {
            offs[i] = sum;
            sum += ((ushort[]) t[INFLATE_TREE_TABLE])[i];
        }

        /* create code->symbol translation table (symbols sorted by code) */
        for (ushort i = 0; i < num; i += 1)
        {
            if (lengths[off + i] != 0)
            {
                //t.trans[offs[lengths[off + i]]++] = i;
                ((ushort[]) t[INFLATE_TREE_TRANS])[offs[lengths[off + i]]] = i;
                offs[lengths[off + i]] += 1;
            }
        }
    }


    /* build extra bits and base tables */
    private void tinf_build_bits_base(byte[] bits, ushort[] bae, byte delta, byte first)
    {
        int i;
        ushort sum;

        /* build bits table */
        for (i = 0; i < delta; ++i)
        {
            bits[i] = 0;
        }

        for (i = 0; i < 30 - delta; ++i)
        {
            bits[i + delta] = (byte) (i / delta | 0x0);
        }

        /* build base table */
        for (sum = first, i = 0; i < 30; ++i)
        {
            bae[i] = sum;
            sum += (ushort) ((1 << bits[i]) & 0xFFFF);
        }
    }

    /* get one bit from source stream */
    private byte INFLATEReadBit(object[] d)
    {
        /* check if tag is empty */
        d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] - 1; // bitcount--
        if ((int) d[INFLATE_DATA_BITCOUNT] == -1)
        {
            /* load next tag */
            d[INFLATE_DATA_TAG] = (int) ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX]];
            d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] + 1;
            d[INFLATE_DATA_BITCOUNT] = 7;
        }

        /* shift bit out of tag */
        var bit = (byte) ((int) d[INFLATE_DATA_TAG] & 1);
        d[INFLATE_DATA_TAG] = (int) d[INFLATE_DATA_TAG] >> 1;
        return bit;
    }

    private int INFLATEReadBits(object[] d, byte num, int bae)
    {
        if (num == 0)
            return bae;

        while ((int) d[INFLATE_DATA_BITCOUNT] < 24)
        {
            // d.tag |= d.source[d.sourceIndex++] << d.bitcount;
            var prevValue = (int) d[INFLATE_DATA_TAG];
            var dataSource = (byte[]) d[INFLATE_DATA_SOURCE];
            var sourceIndex = (int) d[INFLATE_DATA_SOURCE_INDEX];
            var dataSourceValue = sourceIndex >= dataSource.Length ? 0 : dataSource[sourceIndex];
            d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] + 1;
            var bitCount = (int) d[INFLATE_DATA_BITCOUNT];
            d[INFLATE_DATA_TAG] = (prevValue | (dataSourceValue << bitCount));
            // d.bitcount += 8;
            d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] + 8;
        }

        var val = (int) d[INFLATE_DATA_TAG] & (0xFFFF >> (16 - num));
        d[INFLATE_DATA_TAG] = (int) d[INFLATE_DATA_TAG] >> num;
        d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] - num;
        return val + bae;
    }

    /* given a data stream and a tree, decode a symbol */
    private ushort INFLATEDecodeSymbol(object[] d, object[] t)
    {
        while ((int) d[INFLATE_DATA_BITCOUNT] < 24)
        {
            // d.tag |= d.source[d.sourceIndex++] << d.bitcount;
            var prevValue = (int) d[INFLATE_DATA_TAG];
            var dataSource = (byte[]) d[INFLATE_DATA_SOURCE];
            var sourceIndex = (int) d[INFLATE_DATA_SOURCE_INDEX];
            var dataSourceValue = sourceIndex >= dataSource.Length ? 0 : dataSource[sourceIndex];
            d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] + 1;
            var bitCount = (int) d[INFLATE_DATA_BITCOUNT];
            d[INFLATE_DATA_TAG] = (prevValue | (dataSourceValue << bitCount));
            // d.bitcount += 8;
            d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] + 8;
        }

        int sum = 0, cur = 0, len = 0;
        // ReSharper disable once LocalVariableHidesMember
        var tag = (int) d[INFLATE_DATA_TAG];

        // Debug.Log("tag: " + tag);

        // get more bits while code value is above sum
        do
        {
            cur = 2 * cur + (tag & 1);
            tag >>= 1;
            ++len;

            //Debug.Log("tree value is: " + ((ushort[]) t[INFLATE_TREE_TABLE])[len]);

            sum += ((ushort[]) t[INFLATE_TREE_TABLE])[len];
            cur -= ((ushort[]) t[INFLATE_TREE_TABLE])[len];
        } while (cur >= 0);

        d[INFLATE_DATA_TAG] = tag;
        d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] - len;

        var result = ((ushort[]) t[INFLATE_TREE_TRANS])[sum + cur];
        return result;
    }

    /* given a stream and two trees, inflate a block of data */
    private bool INFLATEBlockData(object[] d, object[] lt, object[] dt)
    {
        var iterationCount = 0;
        while (true)
        {
            var sym = INFLATEDecodeSymbol(d, lt);

#if DEBUG
            Debug.Log("We are on iteration " + iterationCount + " " + sym);
#endif
            iterationCount++;

            // check for end of block
            if (sym == 256)
            {
                return true;
            }

            if (sym < 256)
            {
                // d.dest[d.destLen++] = sym;
                ((byte[]) d[INFLATE_DATA_DEST])[(int) d[INFLATE_DATA_DEST_LENGTH]] = (byte) sym;
                d[INFLATE_DATA_DEST_LENGTH] = (int) d[INFLATE_DATA_DEST_LENGTH] + 1;
            }
            else
            {
                sym -= 257;

                // possibly get more bits from length code
                var length = INFLATEReadBits(d, length_bits[sym], length_base[sym]);

                int dist = INFLATEDecodeSymbol(d, dt);

                // possibly get more bits from distance code
                var offs = (int) d[INFLATE_DATA_DEST_LENGTH] - INFLATEReadBits(d, dist_bits[dist], dist_base[dist]);

                // Debug.Log("offset: " + offs + " " + d[INFLATE_DATA_DEST_LENGTH] + " " + dist_bits[dist] + " " + dist_base[dist]);

                // copy match
                for (var i = offs; i < offs + length; ++i)
                {
                    // d.dest[d.destLen++] = d.dest[i];
                    ((byte[]) d[INFLATE_DATA_DEST])[(int) d[INFLATE_DATA_DEST_LENGTH]] =
                        ((byte[]) d[INFLATE_DATA_DEST])[i];
                    d[INFLATE_DATA_DEST_LENGTH] = (int) d[INFLATE_DATA_DEST_LENGTH] + 1;
                }
            }
        }
    }


    /* inflate an uncompressed block of data */
    private bool INFLATEUncompressedBlock(object[] d)
    {
        // unread from bit buffer
        while ((int) d[INFLATE_DATA_BITCOUNT] > 8)
        {
            d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] - 1;
            d[INFLATE_DATA_BITCOUNT] = (int) d[INFLATE_DATA_BITCOUNT] - 8;
        }

        // get length
        int length = ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX] + 1];
        length = 256 * length + ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX]];

        // get ones complement of length
        int invlength = ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX] + 3];
        invlength = 256 * invlength + ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX] + 2];

        // check length
        if (length != ((invlength ^ 0xFFFFFFFF) & 0x0000ffff))
            return false;

        d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] + 4;

        // copy block
        for (int i = length; i != 0; --i)
        {
            // d.dest[d.destLen++] = d.source[d.sourceIndex++];
            ((byte[]) d[INFLATE_DATA_DEST])[(int) d[INFLATE_DATA_DEST_LENGTH]] =
                ((byte[]) d[INFLATE_DATA_SOURCE])[(int) d[INFLATE_DATA_SOURCE_INDEX]];
            d[INFLATE_DATA_DEST_LENGTH] = (int) d[INFLATE_DATA_DEST_LENGTH] + 1;
            d[INFLATE_DATA_SOURCE_INDEX] = (int) d[INFLATE_DATA_SOURCE_INDEX] + 1;
        }

        // make sure we start next block on a byte boundary
        d[INFLATE_DATA_BITCOUNT] = 0;
        return true;
    }

    /* given a data stream, decode dynamic trees from it */
    private void INFLATEDecodeTrees(object[] d, object[] lt, object[] dt)
    {
        /* get 5 bits HLIT (257-286) */
        var hlit = INFLATEReadBits(d, 5, 257);

        /* get 5 bits HDIST (1-32) */
        var hdist = INFLATEReadBits(d, 5, 1);

        /* get 4 bits HCLEN (4-19) */
        var hclen = INFLATEReadBits(d, 4, 4);

        for (var i = 0; i < 19; ++i)
        {
            lengths[i] = 0;
        }

        /* read code lengths for code length alphabet */
        for (var i = 0; i < hclen; ++i)
        {
            /* get 3 bits code length (0-7) */
            var clen = INFLATEReadBits(d, 3, 0);
            lengths[clcidx[i]] = (byte) clen;
        }

        /* build code length tree */
        INFLATEBuildTree(code_tree, lengths, 0, 19);

        /* decode code lengths for the dynamic trees */
        for (var num = 0; num < hlit + hdist;)
        {
            var sym = INFLATEDecodeSymbol(d, code_tree);

            int length;
            switch (sym)
            {
                case 16:
                    /* copy previous code length 3-6 times (read 2 bits) */
                    var prev = lengths[num - 1];
                    for (length = INFLATEReadBits(d, 2, 3); length != 0; --length)
                    {
                        lengths[num++] = prev;
                    }

                    break;
                case 17:
                    /* repeat code length 0 for 3-10 times (read 3 bits) */
                    for (length = INFLATEReadBits(d, 3, 3); length != 0; --length)
                    {
                        lengths[num++] = 0;
                    }

                    break;
                case 18:
                    /* repeat code length 0 for 11-138 times (read 7 bits) */
                    for (length = INFLATEReadBits(d, 7, 11); length != 0; --length)
                    {
                        lengths[num++] = 0;
                    }

                    break;
                default:
                    /* values 0-15 represent the actual code lengths */
                    lengths[num++] = (byte) (sym & 0xFF);
                    break;
            }
        }

        /* build dynamic trees */
        INFLATEBuildTree(lt, lengths, 0, hlit);
        INFLATEBuildTree(dt, lengths, hlit, hdist);
    }

    /*
     * {
     *     0: source data (byte[])
     *     1: source index (int)
     *     2: tag (byte)
     *     3: bitcount (int)
     * }
     */
    private void INFLATE(byte[] source, byte[] dest)
    {
        Debug.Log(Convert.ToBase64String(source));

        var d = new object[8];
        d[INFLATE_DATA_SOURCE] = source;
        d[INFLATE_DATA_SOURCE_INDEX] = 0;
        d[INFLATE_DATA_DEST] = dest;
        d[INFLATE_DATA_DEST_LENGTH] = 0;
        d[INFLATE_DATA_TAG] = 0;
        d[INFLATE_DATA_BITCOUNT] = 0;
        d[INFLATE_DATA_LTREE] = NewEmptyTree();
        d[INFLATE_DATA_DTREE] = NewEmptyTree();

        byte bfinal;
        do
        {
            bfinal = INFLATEReadBit(d);
            // Debug.Log("after final tag: " + d[INFLATE_DATA_TAG]);
            var btype = INFLATEReadBits(d, 2, 0);

#if DEBUG
            Debug.Log("bfinal: " + bfinal);
            Debug.Log("btype: " + btype);
            // Debug.Log("beginning tag: " + d[INFLATE_DATA_TAG]);
#endif

            var status = false;

            switch (btype)
            {
                case 0:
                    /* decompress uncompressed block */
                    status = INFLATEUncompressedBlock(d);
                    break;
                case 1:
                    /* decompress block with fixed huffman trees */
                    status = INFLATEBlockData(d, _sltree, _sdtree);
                    break;
                case 2:
                    /* decompress block with dynamic huffman trees */
                    INFLATEDecodeTrees(d, (object[]) d[INFLATE_DATA_LTREE], (object[]) d[INFLATE_DATA_DTREE]);
                    Debug.Log("Decoded trees complete.");
                    status = INFLATEBlockData(d, (object[]) d[INFLATE_DATA_LTREE],
                        (object[]) d[INFLATE_DATA_DTREE]);
                    break;
                default:
                    Debug.LogError("Invalid compression mode in INFLATE, reserved.");
                    break;
            }

            Debug.Log("Successful decompression: " + status);
        } while (bfinal == 0);
    }

    #endregion INFLATE

    public void Init()
    {
        //
        // Initialize INFLATE code
        //
        _sltree = NewEmptyTree();
        _sdtree = NewEmptyTree();
        // build fixed huffman trees
        tinf_build_fixed_trees(_sltree, _sdtree);
        // build extra bits and base tables
        tinf_build_bits_base(length_bits, length_base, 4, 3);
        tinf_build_bits_base(dist_bits, dist_base, 2, 1);
        // fix a special case (?)
        length_bits[28] = 0;
        length_base[28] = 258;

        hasBeenInit = true;
    }

    #region I/O UTILITY METHODS

    /**********************
     * I/O UTILITY METHODS
     **********************/

    private short ReadShort(byte[] data, int addr)
    {
        var a = data[addr];
        var b = data[addr + 1];
        return (short) (b << 8 | a);
    }

    private int ReadInt(byte[] data, int addr)
    {
        var a = data[addr];
        var b = data[addr + 1];
        var c = data[addr + 2];
        var d = data[addr + 3];
        return d << 24 | c << 16 | b << 8 | a;
    }

    private string ReadString(byte[] data, int addr, int length)
    {
        var b = new char[length];
        for (var i = 0; i != length; i++)
        {
            b[i] = (char) data[addr + i];
        }

        return new String(b);
    }

    private byte[] ReadByteArray(byte[] data, int addr, int length)
    {
        var b = new byte[length];
        for (var i = 0; i != length; i++)
        {
            b[i] = data[addr + i];
        }

        return b;
    }

    #endregion I/O UTILITY METHODS


    private int FindEOCDAddress(byte[] data)
    {
        // 01 02 - start of directory
        // 03 04 - start of file
        // 05 06 - end of directory
        for (var i = data.Length - 4; i >= 0; i--)
        {
            var c = data[i];
            var n = data[i + 1];
            var d1 = data[i + 2];
            var d2 = data[i + 3];
            if (c == (byte) 'P' && n == (byte) 'K' && d1 == 0x05 && d2 == 0x06)
            {
                return i;
            }
        }

        return -1;
    }

    /*
     * {
     *     0: totalNumbersOfCDS (short)
     *     1: sizeOfCentralDirectory (int)
     *     2: centralDirectoryOffset (int)
     *     3: comment (string)
     * }
     */
    private object[] ReadEOCD(byte[] data, int addr)
    {
        var eocd = new object[4];
        eocd[EOCD_TOTAL_CDS] = ReadShort(data, addr + 10); // totalNumbersOfCDS
        eocd[EOCD_SIZE_OF_CD] = ReadInt(data, addr + 12); // sizeOfCentralDirectory
        eocd[EOCD_CD_OFFSET] = ReadInt(data, addr + 16); // centralDirectoryOffset
        var commentLength = ReadShort(data, addr + 20);
        eocd[EOCD_COMMENT] = ReadString(data, addr + 22, commentLength); // comment
        return eocd;
    }


    /*
     * {
     *     0: version (short)
     *     1: version min to extract (short)
     *     2: general purpose bit flag (short)
     *     3: compression method (short)
     *     4: file last modification time (short)
     *     5: file last modification date (short)
     *     6: CRC-32 of uncompressed data (int)
     *     7: compressed size (int)
     *     8: uncompressed size (int)
     *     9: file name length (short) (n)
     *     10: extra field length (short) (m)
     *     11: file comment length (short) (k)
     *     12: internal file attributes (short)
     *     13: external file attributes (int)
     *     14: offset of local file header (int)
     *     15: file name (string) (n)
     *     16: extra field (object) (m)
     *     17: file comment (string) (k)
     *     18: start of next directory (int)
     * }
     */
    private object[] ReadCD(byte[] data, int addr)
    {
        var cd = new object[19];
        cd[CD_VERSION] = ReadShort(data, addr + 4); // version
        cd[CD_MIN_VERSION] = ReadShort(data, addr + 6); // version min to extract
        cd[CD_BITFLAG] = ReadShort(data, addr + 8); // general purpose bit flag
        cd[CD_COMPRESSION_METHOD] = ReadShort(data, addr + 10); // compression method
        cd[CD_LAST_MODIFICATION_TIME] = ReadShort(data, addr + 12); // file last modification time
        cd[CD_LAST_MODIFICATION_DATE] = ReadShort(data, addr + 14); // file last modification date
        cd[CD_CRC_32] = ReadInt(data, addr + 16); // CRC-32 of uncompressed data
        cd[CD_COMPRESSED_SIZE] = ReadInt(data, addr + 20); // compressed size
        cd[CD_UNCOMPRESSED_SIZE] = ReadInt(data, addr + 24); // uncompressed size
        cd[CD_NAME_LENGTH] = ReadShort(data, addr + 28); // file name length
        cd[CD_EXTRA_FIELD_LENGTH] = ReadShort(data, addr + 30); // extra field length
        cd[CD_COMMENT_LENGTH] = ReadShort(data, addr + 32); // file comment length
        cd[CD_INTERNAL_FILE_ATTR] = ReadShort(data, addr + 36); // internal file attributes
        cd[CD_EXTERNAL_FILE_ATTR] = ReadInt(data, addr + 38); // external file attributes
        cd[CD_OFFSET_LFH] = ReadInt(data, addr + 42); // offset of local file header
        cd[CD_NAME] = ReadString(data, addr + 46, (short) cd[9]); // file name
        cd[CD_EXTRA_FIELD] = ReadString(data, addr + 46 + (short) cd[9], (short) cd[10]); // extra field
        cd[CD_COMMENT] = ReadString(data, addr + 46 + (short) cd[9] + (short) cd[10], (short) cd[11]); // file comment
        cd[CD_START_OF_NEXT_CD] =
            addr + 46 + (short) cd[9] + (short) cd[10] + (short) cd[11]; // start of next directory

        return cd;
    }

    /*
     * {
     *     0: version min to extract (short)
     *     1: general purpose bit flag (short)
     *     2: compression method (short)
     *     3: file last modification time (short)
     *     4: file last modification date (short)
     *     5: CRC-32 of uncompressed data (int)
     *     6: compressed size (int)
     *     7: uncompressed size (int)
     *     8: file name length (short) (n)
     *     9: extra field length (short) (m)
     *     10: file name (string) (n)
     *     11: extra field (object) (m)
     *     12: start of data (int)
     * }
     */
    private object[] ReadLFH(byte[] data, int addr)
    {
        var lfh = new object[13];
        lfh[LFH_MIN_VERSION] = ReadShort(data, addr + 4); // version min to extract
        lfh[LFH_BITFLAG] = ReadShort(data, addr + 6); // general purpose bit flag
        lfh[LFH_COMPRESSION_METHOD] = ReadShort(data, addr + 8); // compression method
        lfh[LFH_LAST_MODIFICATION_TIME] = ReadShort(data, addr + 10); // file last modification time
        lfh[LFH_LAST_MODIFICATION_DATE] = ReadShort(data, addr + 12); // file last modification date
        lfh[LFH_CRC_32] = ReadInt(data, addr + 14); // CRC-32 of uncompressed data
        lfh[LFH_COMPRESSED_SIZE] = ReadInt(data, addr + 18); // compressed size
        lfh[LFH_UNCOMPRESSED_SIZE] = ReadInt(data, addr + 22); // uncompressed size
        lfh[LFH_NAME_LENGTH] = ReadShort(data, addr + 26); // file name length
        lfh[LFH_EXTRA_FIELD_LENGTH] = ReadShort(data, addr + 28); // extra field length
        lfh[LFH_NAME] = ReadString(data, addr + 30, (short) lfh[8]); // file name
        lfh[LFH_EXTRA_FIELD] = ReadString(data, addr + 30 + (short) lfh[8], (short) lfh[9]); // extra field
        lfh[LFH_START_OF_DATA] = addr + 30 + (short) lfh[8] + (short) lfh[9]; // start of data

        return lfh;
    }

    /*
     * {
     *     0: eocd
     *     1: [
     *         {
     *             0: cd
     *             1: uncompressed data (byte[]) (null if not uncompressed)
     *             2: compressed data (byte[]) (null if not compressed)
     *         }
     *     ]
     * }
     */

    public object Extract(byte[] data)
    {
        // Check if the INFLATE trees as been set up
        if (!hasBeenInit) Init();

        // Find start EOCD address
        var addrEOCD = FindEOCDAddress(data);

        // Parse the EOCD
        var eocd = ReadEOCD(data, addrEOCD);

        // Build the archive object
        var archive = new object[2];
        archive[ARCHIVE_EOCD] = eocd;
        var entries = new object[(short) eocd[EOCD_TOTAL_CDS]];
        archive[ARCHIVE_ENTIRES] = entries;

        // Reads all CentralDirectories
        var addrOfLastDirectory = (int) eocd[EOCD_CD_OFFSET];
        for (var cdi = 0; cdi != (short) eocd[0]; cdi++)
        {
            var cd = ReadCD(data, addrOfLastDirectory);
            addrOfLastDirectory = (int) cd[CD_START_OF_NEXT_CD];

            var lfh = ReadLFH(data, (int) cd[CD_OFFSET_LFH]);
#if DEBUG
            Debug.Log("File name: " + lfh[LFH_NAME]);
            Debug.Log("Compressed size: " + lfh[LFH_COMPRESSED_SIZE]);
            Debug.Log("Uncompressed size: " + lfh[LFH_UNCOMPRESSED_SIZE]);
            Debug.Log("Compression method: " + lfh[LFH_COMPRESSION_METHOD]);
#endif

            var fileEntry = new object[3];
            fileEntry[FILEENTRY_CD] = cd;
            fileEntry[FILEENTRY_UNCOMPRESSED] = null;
            fileEntry[FILEENTRY_COMPRESSED] = null;

            if ((short) lfh[LFH_COMPRESSION_METHOD] == COMPRESSION_METHOD_NONE)
            {
                var fileData = ReadByteArray(data, (int) lfh[LFH_START_OF_DATA], (int) lfh[LFH_UNCOMPRESSED_SIZE]);
                fileEntry[FILEENTRY_UNCOMPRESSED] = fileData;
            }
            else if ((short) lfh[LFH_COMPRESSION_METHOD] == COMPRESSION_METHOD_INFLATE)
            {
                var fileData = ReadByteArray(data, (int) lfh[LFH_START_OF_DATA], (int) lfh[LFH_COMPRESSED_SIZE]);
                fileEntry[FILEENTRY_COMPRESSED] = fileData;
            }
            else
            {
                Debug.LogError("Unsupported compression method: " + (short) lfh[LFH_COMPRESSION_METHOD]);
                Die();
            }

            entries[cdi] = fileEntry;
        }

        return archive;
    }

    public string[] GetFileNames(object archive)
    {
        var entries = (object[]) ((object[]) archive)[ARCHIVE_ENTIRES];
        var fileNames = new string[entries.Length];
        for (var i = 0; i != entries.Length; i++)
        {
            var entry = (object[]) entries[i];
            var cd = (object[]) entry[FILEENTRY_CD];
            var fileName = (string) cd[CD_NAME];
            fileNames[i] = fileName;
        }
        return fileNames;
    }

    public object GetFile(object archive, string filePath)
    {
        var entries = (object[]) ((object[]) archive)[ARCHIVE_ENTIRES];
        for (var i = 0; i != entries.Length; i++)
        {
            var entry = (object[]) entries[i];
            var cd = (object[]) entry[FILEENTRY_CD];
            var fileName = (string) cd[CD_NAME];
            if (fileName == filePath)
            {
                return entry;
            }
        }

        return null;
    }

    // ReSharper disable once ReturnTypeCanBeEnumerable.Global
    public byte[] GetFileData(object file)
    {
        // Check if the file is already uncompressed, if so, just return it.
        var fileEntry = (object[]) file;
        if (fileEntry[FILEENTRY_UNCOMPRESSED] != null)
        {
            return (byte[]) fileEntry[FILEENTRY_UNCOMPRESSED];
        }

        // If was not uncompressed, lets decompress it.
        var cd = (object[]) fileEntry[FILEENTRY_CD];
        var uncompressedData = new byte[(int) cd[CD_UNCOMPRESSED_SIZE]];
        var fileData = (byte[]) fileEntry[FILEENTRY_COMPRESSED];

        // Switch depending on un-compression method
        if ((short) cd[CD_COMPRESSION_METHOD] == COMPRESSION_METHOD_INFLATE)
        {
            INFLATE(fileData, uncompressedData);
        }
        else
        {
            Debug.LogError("Unsupported compression method: " + (short) cd[CD_COMPRESSION_METHOD]);
            Die();
        }

        fileEntry[FILEENTRY_UNCOMPRESSED] = uncompressedData;
        return uncompressedData;
    }


    private void Die()
    {
        // ReSharper disable once PossibleNullReferenceException
        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
        ((string) null).ToString();
    }
}
