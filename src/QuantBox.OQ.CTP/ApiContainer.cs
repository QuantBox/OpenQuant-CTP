using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantBox.OQ.CTP
{
    public class ApiContainer<T>
    {
        private Dictionary<string, T> string_T = new Dictionary<string, T>();
        private Dictionary<IntPtr, T> IntPtr_T = new Dictionary<IntPtr, T>();

        public T this[string account]
        {
            get
            {
                return string_T[account];
            }
            //set
            //{
            //    string_T[account] = value;
            //}
        }

        public T this[IntPtr ptr]
        {
            get
            {
                return IntPtr_T[ptr];
            }
            //set
            //{
            //    IntPtr_T[ptr] = value;
            //}
        }

        public void Add(string account, T t)
        {
            string_T[account] = t;
        }

        public void Add(IntPtr ptr, T t)
        {
            IntPtr_T[ptr] = t;
        }

        public void Remove(IntPtr ptr)
        {
        }
        public void Remove(string account)
        {
        }
        public void Remove(ApiWrapper api)
        {
        }
        public void Remove(int index)
        {
        }
    }
}
