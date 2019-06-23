using System;
using System.Collections.Generic;
using System.Text;

namespace FileSysTemp.FSBase
{
    public class Component
    {
        //间址寻址结构
        public abstract class Data
        {
            public abstract int getItem(uint i = 0, uint j = 0);
        }
        public class DataInt : Data
        {
            int adata;
            public DataInt(int a)
            {
                adata = a;
            }
            public override int getItem(uint i = 0, uint j = 0)
            {
                return adata;
            }
        }
        public class DataIntArray : Data
        {
            public int[] data = new int[24];
            public override int getItem(uint i = 0, uint j = 0)
            {
                return data[i];
            }
            public DataIntArray(int key, int value)
            {
                data[key] = value;
            }
        }
        public class DataIntArrayArray : Data
        {
            public int[,] data = new int[24, 24];
            public override int getItem(uint i = 0, uint j = 0)
            {
                return data[i, j];
            }
            public DataIntArrayArray(int key1, int key2, int value)
            {
                data[key1, key2] = value;
            }
        }
    }
}
