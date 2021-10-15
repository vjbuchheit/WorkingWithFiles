﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using BaseLibrary;
using Microsoft.VisualBasic.FileIO;

namespace WindowsFormsApp1.Classes
{
    /// <summary>
    /// Presents several methods to parse a .csv file with no external libraries
    /// There are external libraries such as https://joshclose.github.io/CsvHelper/ which
    /// is not covered here as the intent is for work solely with Microsoft classes.
    /// </summary>
    public class FileOperations : BaseExceptionProperties
    {
        /*
         *This would normally be passed in from using a pre-defined list in a ListBox/ComboBox or from OpenDialog.
         */
        private readonly string _inputFileName = 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SacramentocrimeJanuary2006.csv");

        /// <summary>
        /// Load file via OleDb
        /// </summary>
        /// <remarks>
        /// This method is fine if each column data is the correct type,
        /// if not then a manual parse (as shown with StreamReader and TextFieldParser are better chooses)
        /// </remarks>
        public (DataTable table, Exception exception) LoadCsvFileOleDb()
        {
            var connString = 
                $@"Provider=Microsoft.Jet.OleDb.4.0; " + 
                $"Data Source={Path.GetDirectoryName(_inputFileName)};Extended Properties=\"Text;HDR=YES;FMT=Delimited\"";

            var table = new DataTable();

            try
            {
                using (var cn = new OleDbConnection(connString))
                {
                    cn.Open();

                    var selectStatement = "SELECT * FROM [" + Path.GetFileName(_inputFileName) + "]";

                    using (var adapter = new OleDbDataAdapter(selectStatement, cn))
                    {
                        var ds = new DataSet("Demo");
                        adapter.Fill(ds);

                        ds.Tables[0].TableName = Path.GetFileNameWithoutExtension(_inputFileName);
                        table = ds.Tables[0];

                    }
                }
            }
            catch (Exception ex)
            {
                return (null, ex);
            }

            return (table, null);
        }

        /// <summary>
        /// Load file via StreamReader
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public (bool Success, List<DataItem>, List<DataItemInvalid>) LoadCsvFileStreamReader(string filePath = "")
        {
            if (!File.Exists(_inputFileName))
            {
                mHasException = true;
                mLastException = new FileNotFoundException($"Missing {_inputFileName}");
                return (mHasException, new List<DataItem>(),new List<DataItemInvalid>() );
            }

            mHasException = false;

            var validRows = new List<DataItem>();
            var invalidRows = new List<DataItemInvalid>();
            var validateBad = 0;

            int index = 0;

            int district = 0;
            int grid = 0;
            int nCode = 0;
            float latitude = 0;
            float longitude = 0;

            try
            {
                using (var readFile = new StreamReader(_inputFileName))
                {
                    string line;
                    string[] parts;

                    while ((line = readFile.ReadLine()) != null)
                    {
                        parts = line.Split(',');
                        index += 1;

                        if (parts == null)
                        {
                            break;
                        }

                        index += 1;
                        validateBad = 0;

                        if (parts.Length != 9)
                        {
                            invalidRows.Add(new DataItemInvalid() { Row = index, Line = string.Join(",", parts) });
                            continue;

                        }

                        // Skip first row which in this case is a header with column names
                        if (index <= 1) continue;
                        /*
                         * These columns are checked for proper types
                         */
                        var validRow = DateTime.TryParse(parts[0], out var d) &&
                                       float.TryParse(parts[7].Trim(), out latitude) &&
                                       float.TryParse(parts[8].Trim(), out longitude) &&
                                       int.TryParse(parts[2], out district) &&
                                       int.TryParse(parts[4], out grid) &&
                                       !string.IsNullOrWhiteSpace(parts[5]) &&
                                       int.TryParse(parts[6], out nCode);

                        /*
                         * Questionable fields
                         */
                        if (string.IsNullOrWhiteSpace(parts[1]))
                        {
                            validateBad += 1;
                        }
                        if (string.IsNullOrWhiteSpace(parts[3]))
                        {
                            validateBad += 1;
                        }

                        // NICI code must be 909 or greater
                        if (nCode < 909)
                        {
                            validateBad += 1;
                        }

                        if (validRow)
                        {

                            validRows.Add(new DataItem()
                            {
                                Id = index,
                                Date = d,
                                Address = parts[1],
                                District = district,
                                Beat = parts[3],
                                Grid = grid,
                                Description = parts[5],
                                NcicCode = nCode,
                                Latitude = latitude,
                                Longitude = longitude,
                                Inspect = validateBad > 0
                            });

                        }
                        else
                        {
                            // fields to review in specific rows
                            invalidRows.Add(new DataItemInvalid() { Row = index, Line = string.Join(",", parts) });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                mHasException = true;
                mLastException = ex;
            }

            return (IsSuccessFul, validRows, invalidRows);

        }
        /// <summary>
        /// Load file via VB TextFieldParser
        /// </summary>
        /// <returns></returns>
        public (bool Success, List<DataItem>, List<DataItemInvalid>, int EmptyLineCount) LoadCsvFileTextFieldParser()
        {
            mHasException = false;

            var validRows = new List<DataItem>();
            var invalidRows = new List<DataItemInvalid>();
            var validateBad = 0;

            int index = 0;

            int district = 0;
            int grid = 0;
            int ucrNcicCode = 0;
            float latitude = 0;
            float longitude = 0;

            var emptyLineCount = 0;
            var line = "";

            try
            {
                /*
                 * If interested in blank line count
                 */
                using (var reader = File.OpenText(_inputFileName))
                {
                    while ((line = reader.ReadLine()) != null) // EOF
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            emptyLineCount++;
                        }
                    }
                }

                using (var parser = new TextFieldParser(_inputFileName))
                {
                    
                    parser.Delimiters = new[] { "," };
                    while (true)
                    {

                        string[] parts = parser.ReadFields();

                        if (parts == null)
                        {
                            break;
                        }

                        index += 1;
                        validateBad = 0;

                        if (parts.Length != 9)
                        {
                            
                            invalidRows.Add(new DataItemInvalid()
                            {
                                Row = index, 
                                Line = string.Join(",", parts)
                            });

                            continue;

                        }

                        // Skip first row which in this case is a header with column names
                        if (index <= 1) continue;

                        /*
                         * These columns are checked for proper types
                         */
                        var validRow = 
                            DateTime.TryParse(parts[0], out var cdatetime) && 
                            float.TryParse(parts[7].Trim(), out latitude) && 
                            float.TryParse(parts[8].Trim(), out longitude) && 
                            int.TryParse(parts[2], out district) && 
                            int.TryParse(parts[4], out grid) && 
                            !string.IsNullOrWhiteSpace(parts[5]) && 
                            int.TryParse(parts[6], out ucrNcicCode);

                        /*---------------------------------------------------------------
                         * Questionable fields
                         ---------------------------------------------------------------*/
                        if (string.IsNullOrWhiteSpace(parts[1]))
                        {
                            validateBad += 1;
                        }
                        if (string.IsNullOrWhiteSpace(parts[3]))
                        {
                            validateBad += 1;
                        }

                        // NICI code must be 909 or greater
                        if (ucrNcicCode < 909)
                        {
                            validateBad += 1;
                        }

                        if (validRow)
                        {

                            validRows.Add(new DataItem()
                            {
                                Id = index,
                                Date = cdatetime,
                                Address = parts[1],
                                District = district,
                                Beat = parts[3],
                                Grid = grid,
                                Description = parts[5],
                                NcicCode = ucrNcicCode,
                                Latitude = latitude,
                                Longitude = longitude,
                                Inspect = validateBad > 0
                            });

                        }
                        else
                        {
                            // fields to review in specific rows
                            invalidRows.Add(new DataItemInvalid()
                            {
                                Row = index, 
                                Line = string.Join(",", parts)
                            });
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                mHasException = true;
                mLastException = ex;
            }

            return (IsSuccessFul, validRows, invalidRows,emptyLineCount);

        }

    }
}
