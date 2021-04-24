﻿using BigCookieKit.XML;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace BigCookieKit.Office
{
    /// <summary>
    /// Excal读写 只适用xlsx(性能是NPOI的几倍)
    /// </summary>
    public class ReadExcelKit : XmlReadKit
    {
        /// <summary>
        /// 执行Log
        /// </summary>
        public List<string> ExecuteLog { get; set; }

        /// <summary>
        /// Excel分解成zip文件
        /// </summary>
        private ZipArchive zip { get; set; }

        /// <summary>
        /// 共享字符串数组
        /// </summary>
        private List<string> sharedStrings { get; set; }

        /// <summary>
        /// 单元格样式对应的数据格式
        /// </summary>
        private List<string> cellXfs { get; set; }

        /// <summary>
        /// 数值类型格式
        /// </summary>
        private Dictionary<string, string> numFmts { get; set; }

        /// <summary>
        /// 工作簿
        /// </summary>
        private List<WorkBook> wookbooks { get; set; }

        /// <summary>
        /// Excel配置
        /// </summary>
        private List<ExcelConfig> configs { get; set; }

        /// <summary>
        /// 当前配置
        /// </summary>
        private ExcelConfig current { get; set; }

        private Dictionary<string, XDocument> cache { get; set; }
        private Dictionary<string, XmlPacket> xmlcache { get; set; }

        public ReadExcelKit(string filePath)
        {
            cache = new Dictionary<string, XDocument>();

            xmlcache = new Dictionary<string, XmlPacket>();

            configs = new List<ExcelConfig>();

            if (string.IsNullOrEmpty(filePath)) throw new FileNotFoundException(nameof(filePath));

            ExecuteLog = new List<string>();

            zip = ZipFile.OpenRead(filePath);

            LoadShareString();

            LoadWorkBook();

            LoadNumberFormat();

            LoadCellStyle();
        }

        /// <summary>
        /// 创建Excel配置(DataTable用)
        /// </summary>
        /// <param name="callback"></param>
        [Obsolete("Please invoke AddConfig")]
        public void CreateConfig(Action<ExcelConfig> callback)
        {
            var config = new ExcelConfig();
            config.StartColumnIndex = 0;
            config.StartRow = 0;
            callback.Invoke(config);
            configs.Add(config);
        }

        /// <summary>
        /// 增加配置
        /// </summary>
        /// <param name="callback"></param>
        public void AddConfig(Action<ExcelConfig> callback)
        {
            var config = new ExcelConfig();
            config.StartColumnIndex = 0;
            config.StartRow = 0;
            callback.Invoke(config);
            configs.Add(config);
        }

        /// <summary>
        /// 加载共享字符串
        /// </summary>
        private void LoadShareString()
        {
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry != null)
            {
                var doc = XDocument.Load(entry.Open());

                var elements = doc.Root.Elements();

                if (elements.IsNull()) return;

                sharedStrings = new List<string>();

                foreach (var element in elements)
                {
                    sharedStrings.Add(element.Value);
                }
            }
        }

        /// <summary>
        /// 加载数值类型
        /// </summary>
        private void LoadNumberFormat()
        {
            var entry = zip.GetEntry("xl/styles.xml");
            if (entry != null)
            {
                var doc = XDocument.Load(entry.Open());

                var elements = doc.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "numFmts")?.Elements();

                if (elements.IsNull()) return;

                numFmts = new Dictionary<string, string>();

                foreach (var element in elements)
                {
                    numFmts.Add(element.Attribute("numFmtId").Value,
                      element.Attribute("formatCode").Value);
                }
            }
        }

        /// <summary>
        /// 获取单元格样式
        /// </summary>
        private void LoadCellStyle()
        {
            var entry = zip.GetEntry("xl/styles.xml");
            if (entry != null)
            {
                var doc = XDocument.Load(entry.Open());

                var elements = doc.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "cellXfs")?.Elements();

                if (elements.IsNull()) return;

                cellXfs = new List<string>();

                foreach (var element in elements)
                {
                    cellXfs.Add(element.Attribute("numFmtId").Value);
                }
            }
        }

        /// <summary>
        /// 获取工作簿
        /// </summary>
        private void LoadWorkBook()
        {
            var entry = zip.GetEntry("xl/workbook.xml");
            if (entry != null)
            {
                var doc = XDocument.Load(entry.Open());

                wookbooks = new List<WorkBook>();

                var elements = doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "sheets").Elements();

                if (elements.IsNull()) return;

                foreach (var element in elements)
                {
                    wookbooks.Add(new WorkBook()
                    {
                        SheetId = Int32.Parse(element.Attribute("sheetId").Value),
                        SheetName = element.Attribute("name").Value,
                    });
                }
            }
        }

        /// <summary>
        /// 获取所有工作簿
        /// </summary>
        /// <returns></returns>
        public List<WorkBook> GetWorkBook()
        {
            return wookbooks;
        }

        /// <summary>
        /// 获取数据表集合
        /// </summary>
        /// <returns></returns>
        public DataSet ReadDataSet()
        {
            DataSet dataSet = new DataSet();
            foreach (var config in configs)
            {
                current = config;
                try
                {
                    dataSet.Tables.Add(XmlReadDataTable());
                }
                catch (Exception ex)
                {
                    ExecuteLog.Add($"[ReadDataTable]:[{ex.Message}]");
                }
            }
            return dataSet;
        }

        /// <summary>
        /// 获取列英文索引
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        IEnumerable<char> CellPosition(string position)
        {
            for (int i = 0; i < position.Length; i++)
            {
                if (position[i] >= 'A' && position[i] <= 'Z')
                    yield return position[i];
            }
        }

        #region XmlReader方案

        /// <summary>
        /// 读取数据表
        /// </summary>
        /// <returns></returns>

        public DataTable XmlReadDataTable()
        {
            current = current ?? configs.FirstOrDefault();

            var sheet = $"sheet{current.SheetIndex}";

            var entry = zip.GetEntry($"xl/worksheets/{sheet}.xml");

            DataTable dt = new DataTable();
            DataRow ndr = default;
            IDictionary<int, string> Columns = new Dictionary<int, string>();

            XmlReadKit xmlReadKit = new XmlReadKit(entry.Open());
            bool readColumns = false;
            bool readData = false;
            bool isValue = false;

            int rowIndex = default;
            int colIndex = default;
            string dataType = default;
            string xfs = default;
            xmlReadKit.XmlReadXlsx("sheetData", (node, attrs, content) =>
            {
                switch (node)
                {
                    case "end":
                        if (ndr != null) dt.Rows.Add(ndr);
                        break;
                    case "row":
                        rowIndex = int.Parse(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text);
                        if (current.StartRow <= current.ColumnNameRow) throw new XlsxRowConfigException();
                        if (rowIndex == current.ColumnNameRow)
                            readColumns = true;
                        if (rowIndex > current.EndRow)
                            return false;
                        if (rowIndex >= current.StartRow)
                        {
                            readColumns = false;
                            readData = true;
                            if (ndr != null) dt.Rows.Add(ndr);
                            ndr = dt.NewRow();
                        }
                        break;
                    case "c":
                        if (readColumns || readData)
                        {
                            string colEn = new string(CellPosition(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text).ToArray());
                            colIndex = ExcelHelper.ColumnToIndex(colEn).Value;
                            dataType = attrs.FirstOrDefault(x => x.Name.Equals("t", StringComparison.OrdinalIgnoreCase)).Text;
                            xfs = attrs.FirstOrDefault(x => x.Name.Equals("s", StringComparison.OrdinalIgnoreCase)).Text;
                        }
                        break;
                    case "v":
                        if (colIndex >= current.StartColumnIndex
                        && (current.EndColumnIndex != null ? colIndex <= current.EndColumnIndex : true))
                            isValue = true;
                        break;
                    case "f":
                        break;
                    case "text":
                        if (isValue)
                        {
                            if (readColumns)
                            {
                                if (dataType == "s")
                                {
                                    dt.Columns.Add(sharedStrings[int.Parse(content)], typeof(string));
                                    Columns.Add(colIndex, sharedStrings[int.Parse(content)]);
                                }
                                else
                                {
                                    dt.Columns.Add(content, typeof(string));
                                    Columns.Add(colIndex, content);
                                }
                            }
                            if (readData)
                            {
                                if (dataType == "s")
                                    ndr[Columns[colIndex]] = sharedStrings[int.Parse(content)];
                                else
                                    ndr[Columns[colIndex]] = content;
                            }
                            isValue = false;
                        }
                        break;
                    default:
                        isValue = false;
                        break;
                }
                return true;
            });
            return dt;
        }

        public IEnumerable<object[]> XmlReaderSet()
        {
            current = current ?? configs.FirstOrDefault();

            var sheet = $"sheet{current.SheetIndex}";

            var entry = zip.GetEntry($"xl/worksheets/{sheet}.xml");

            List<object[]> list = new List<object[]>();
            List<object> temp = null;

            XmlReadKit xmlReadKit = new XmlReadKit(entry.Open());
            bool readData = false;
            bool isValue = false;

            int rowIndex = default;
            int colIndex = default;
            string dataType = default;
            string xfs = default;
            xmlReadKit.XmlReadXlsx("sheetData", (node, attrs, content) =>
            {
                switch (node)
                {
                    case "end":
                        if (temp != null) list.Add(temp.ToArray());
                        break;
                    case "row":
                        rowIndex = int.Parse(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text);
                        if (rowIndex > current.EndRow)
                            return false;
                        if (rowIndex >= current.StartRow)
                        {
                            readData = true;
                            if (temp != null) list.Add(temp.ToArray());
                            temp = new List<object>();
                        }
                        break;
                    case "c":
                        if (readData)
                        {
                            string colEn = new string(CellPosition(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text).ToArray());
                            colIndex = ExcelHelper.ColumnToIndex(colEn).Value;
                            dataType = attrs.FirstOrDefault(x => x.Name.Equals("t", StringComparison.OrdinalIgnoreCase)).Text;
                            xfs = attrs.FirstOrDefault(x => x.Name.Equals("s", StringComparison.OrdinalIgnoreCase)).Text;
                        }
                        break;
                    case "v":
                        if (colIndex >= current.StartColumnIndex
                        && (current.EndColumnIndex != null ? colIndex <= current.EndColumnIndex : true))
                            isValue = true;
                        break;
                    case "f":
                        break;
                    case "text":
                        if (isValue)
                        {
                            if (readData)
                            {
                                if (dataType == "s")
                                    temp.Add(sharedStrings[int.Parse(content)]);
                                else
                                    temp.Add(content);
                            }
                            isValue = false;
                        }
                        break;
                    default:
                        isValue = false;
                        break;
                }
                return true;
            });

            return list;
        }

        public IEnumerable<IDictionary<string, object>> XmlReaderDictionary()
        {
            current = current ?? configs.FirstOrDefault();

            var sheet = $"sheet{current.SheetIndex}";

            var entry = zip.GetEntry($"xl/worksheets/{sheet}.xml");

            List<IDictionary<string, object>> dicList = new List<IDictionary<string, object>>();
            IDictionary<int, string> Columns = new Dictionary<int, string>();
            IDictionary<string, object> temp = null;

            XmlReadKit xmlReadKit = new XmlReadKit(entry.Open());
            bool readColumns = false;
            bool readData = false;
            bool isValue = false;

            int rowIndex = default;
            int colIndex = default;
            string dataType = default;
            string xfs = default;
            xmlReadKit.XmlReadXlsx("sheetData", (node, attrs, content) =>
            {
                switch (node)
                {
                    case "end":
                        if (temp != null) dicList.Add(temp);
                        break;
                    case "row":
                        rowIndex = int.Parse(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text);
                        if (current.StartRow <= current.ColumnNameRow) throw new XlsxRowConfigException();
                        if (rowIndex == current.ColumnNameRow)
                            readColumns = true;
                        if (rowIndex > current.EndRow)
                            return false;
                        if (rowIndex >= current.StartRow)
                        {
                            readColumns = false;
                            readData = true;
                            if (temp != null) dicList.Add(temp);
                            temp = new Dictionary<string, object>();
                        }
                        break;
                    case "c":
                        if (readColumns || readData)
                        {
                            string colEn = new string(CellPosition(attrs.SingleOrDefault(x => x.Name.Equals("r", StringComparison.OrdinalIgnoreCase)).Text).ToArray());
                            colIndex = ExcelHelper.ColumnToIndex(colEn).Value;
                            dataType = attrs.FirstOrDefault(x => x.Name.Equals("t", StringComparison.OrdinalIgnoreCase)).Text;
                            xfs = attrs.FirstOrDefault(x => x.Name.Equals("s", StringComparison.OrdinalIgnoreCase)).Text;
                        }
                        break;
                    case "v":
                        if (colIndex >= current.StartColumnIndex
                        && (current.EndColumnIndex != null ? colIndex <= current.EndColumnIndex : true))
                            isValue = true;
                        break;
                    case "f":
                        break;
                    case "text":
                        if (isValue)
                        {
                            if (readColumns)
                            {
                                if (dataType == "s")
                                {
                                    Columns.Add(colIndex, sharedStrings[int.Parse(content)]);
                                }
                                else
                                {
                                    Columns.Add(colIndex, content);
                                }
                            }
                            if (readData)
                            {
                                if (dataType == "s")
                                {
                                    temp.Add(Columns[colIndex], sharedStrings[int.Parse(content)]);
                                }
                                else
                                {
                                    temp.Add(Columns[colIndex], content);
                                }
                            }
                            isValue = false;
                        }
                        break;
                    default:
                        isValue = false;
                        break;
                }
                return true;
            });

            return dicList;
        }

        /// <summary>
        /// 获取表头
        /// </summary>
        /// <param name="col"></param>
        /// <param name="type"></param>
        /// <returns></returns>

        DataColumn FormColumn(XmlPacket col, string type)
        {
            try
            {
                var value = FromValue(col, type);
                if (value == DBNull.Value)
                {
                    return new DataColumn();
                }
                return new DataColumn(value.ToString(), value.GetType());
            }
            catch (Exception ex)
            {
                ExecuteLog.Add($"[FormColumn]:[{ex.Message}]");
                return new DataColumn();
            }
        }

        object FromValue(XmlPacket col, string type)
        {
            try
            {
                //共享字符串
                if (type == "s")
                    return sharedStrings[int.Parse(GetV(col))];
                //字符串
                if (type == "str")
                    return GetV(col);

                //索引数值类型格式
                var style = col.GetAttr("s").Text;
                if (style.NotNull())
                {
                    var numFmt = cellXfs[int.Parse(style)];
                    var fixedValue = FixedFormat(col, numFmt);
                    //未命中固定样式
                    if (fixedValue.IsNull())
                    {
                        var format = numFmts?[numFmt];
                        return ParseFormat(col, format);
                    }
                    else
                    {
                        return fixedValue;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExecuteLog.Add($"[FromValue]:[{ex.Message}]");
            }

            return DBNull.Value;
        }

        object FixedFormat(XmlPacket col, string numFmt)
        {
            try
            {
                if (ExcelSSF.FixedNumFmt.ContainsKey(numFmt))
                {
                    var type = ExcelSSF.FixedNumFmt[numFmt];
                    if (type == typeof(DateTime))
                    {
                        return fmtParseDateTime(GetV(col));
                    }
                    else if (GetV(col).TryParse(type, out object value))
                    {
                        return value;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExecuteLog.Add($"[FixedFormat]:[{ex.Message}]");
            }

            return null;
        }

        object ParseFormat(XmlPacket col, string format)
        {
            try
            {
                //判断是否是浮点数
                if (format.AllOwn("0", "0."))
                {
                    if (GetV(col).TryParse<decimal>(out object value))
                    {
                        return value;
                    }
                }
                //判断是否是整数型
                if (format.AllOwn("0"))
                {
                    if (GetV(col).TryParse<int>(out object value1))
                    {
                        return value1;
                    }
                    if (GetV(col).TryParse<long>(out object value2))
                    {
                        return value2;
                    }
                }
                //判断是否是日期格式
                if (format.AllOwn("y", "m") || format.AllOwn("m", "d"))
                {
                    return fmtParseDateTime(GetV(col));
                }
                //判断是否是时间格式
                if (format.AllOwn("h", "m"))
                {
                    return fmtParseDateTime(GetV(col));
                }
            }
            catch (System.Exception ex)
            {
                ExecuteLog.Add($"[ParseFormat]:[{ex.Message}]");
            }

            return DBNull.Value;
        }

        string GetV(XmlPacket element)
        {
            return element.Node.FirstOrDefault(x => x.Info.Name.Equals("v", StringComparison.OrdinalIgnoreCase)).Info.Text;
        }

        #endregion

        #region SSF处理

        DateTime excelDateTime = DateTime.Parse("1900-01-01");
        /// <summary>
        /// Excel的时间处理
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        DateTime fmtParseDateTime(string value)
        {
            return excelDateTime.AddDays(double.Parse(value));
        }

        #endregion
    }
}