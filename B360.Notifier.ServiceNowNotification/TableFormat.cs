using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B360.Notifier.ServiceNowNotification
{
    public class TableFormat
    {
        public enum Align { Left, Right };
        private string[] headers;
        private Align CellAlignment = Align.Left;
        private int tableYStart = 0;
        /// <summary>
        /// The last line of the table (gotton from Console.CursorTop). -1 = No printed data
        /// </summary>
        public int LastPrintEnd = -1;

        /// <summary>
        /// Helps create a table
        /// </summary>
        /// <param name="TableStart">What line to start the table on.</param>
        /// <param name="Alignment">The alignment of each cell\'s text.</param>
        public TableFormat(int TableStart, Align Alignment, string[] headersi)
        {
            headers = headersi;
            CellAlignment = Alignment;
            tableYStart = TableStart;
        }
        public void ClearData()
        {
            //Clear Previous data
            if (LastPrintEnd != -1) //A set of data has already been printed
            {
                for (int i = tableYStart; i < LastPrintEnd; i++)
                {
                    ClearLine(i);
                }
            }
            LastPrintEnd = -1;
        }
        public void RePrint(ArrayList data)
        {
            //Set buffers
            if (data.Count > Console.BufferHeight)
                Console.BufferHeight = data.Count;
            //Clear Previous data
            ClearData();

            Console.CursorTop = tableYStart;
            Console.CursorLeft = 0;
            if (data.Count == 0)
            {
                Console.WriteLine("No Records");
                LastPrintEnd = Console.CursorTop;
                return;
            }

            //Get max lengths on each column
            int ComWidth = ((string[])data[0]).Length * 2 + 1;
            int[] ColumnLengths = new int[((string[])data[0]).Length];

            foreach (string[] row in data)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i].Length > ColumnLengths[i])
                    {
                        ComWidth -= ColumnLengths[i];
                        ColumnLengths[i] = row[i].Length;
                        ComWidth += ColumnLengths[i];
                    }
                }
            }
            //Don't forget to check headers
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Length > ColumnLengths[i])
                {
                    ComWidth -= ColumnLengths[i];
                    ColumnLengths[i] = headers[i].Length;
                    ComWidth += ColumnLengths[i];
                }
            }


            if (Console.BufferWidth < ComWidth)
                Console.BufferWidth = ComWidth + 1;
            PrintLine(ComWidth);
            //Print Data
            bool first = true;
            foreach (string[] row in data)
            {
                if (first)
                {
                    //Print Header
                    PrintRow(headers, ColumnLengths);
                    PrintLine(ComWidth);
                    first = false;
                }
                PrintRow(row, ColumnLengths);
            }
            PrintLine(ComWidth);
            LastPrintEnd = Console.CursorTop;
        }

        private void ClearLine(int line)
        {
            int oldtop = Console.CursorTop;
            Console.CursorTop = line;
            int oldleft = Console.CursorLeft;
            Console.CursorLeft = 0;
            int top = Console.CursorTop;

            while (Console.CursorTop == top)
            { Console.Write(" "); }
            Console.CursorLeft = oldleft;
            Console.CursorTop = oldtop;
        }

        private void PrintLine(int width)
        {
            Console.WriteLine(new string('-', width));
        }

        private void PrintRow(string[] row, int[] Widths)
        {
            string s = "|";
            for (int i = 0; i < row.Length; i++)
            {
                if (CellAlignment == Align.Left)
                    s += row[i] + new string(' ', Widths[i] - row[i].Length + 1) + "|";
                else if (CellAlignment == Align.Right)
                    s += new string(' ', Widths[i] - row[i].Length + 1) + row[i] + "|";
            }
            if (s == "|")
                throw new Exception("PrintRow input must not be empty");

            Console.WriteLine(s);
        }
    }
}
