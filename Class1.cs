﻿using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace owin_google_driver_ocr
{

    public enum OCR_DATA_TYPE
    {
        NONE,
        NUMBER_0_9,
        DATE_TIME_BIRTHDAY,
        DATE_TIME_EXPIRY,
        CMT_ID,
        CMT_FULLNAME,
        CMT_BIRTHDAY,
        CMT_EXPIRY,
        CMT_ADDRESS,
    }

    public class OcrConfig
    {
        static readonly string[] ARR_CITY = new string[] { "tuyen quang", "ho chi minh", "thai nguyen", "tien giang", "quang ninh", "quang ngai", "quang binh", "ninh thuan", "kien giang", "binh thuan", "binh phuoc", "binh duong", "vinh phuc", "vinh long", "thanh hoa", "thai binh", "soc trang", "quang tri", "quang nam", "ninh binh", "khanh hoa", "hau giang", "hai phong", "hai duong", "dong thap", "dien bien", "binh dinh", "bac giang", "tra vinh", "tay ninh", "nam dinh", "lang son", "lam dong", "lai chau", "hung yen", "hoa binh", "ha giang", "dong nai", "dak nong", "dak nong", "cao bang", "bac ninh", "bac lieu", "vung tau", "an giang", "yen bai", "phu yen", "phu tho", "nghe an", "long an", "lao cai", "kon tum", "ha tinh", "gia lai", "dak lak", "da nang", "can tho", "ben tre", "bac kan", "son la", "ha noi", "ha nam", "ca mau", "hue" };

        public int Page { set; get; }
        public bool Success { set; get; }
        public string Error { set; get; }
        public string Result { set; get; }
        public string TextSplit { set; get; }
        public OCR_DATA_TYPE Type { set; get; }
        public string Input { set; get; }

        public OcrConfig(string text, OCR_DATA_TYPE type, string textSplit = "")
        {
            Type = type;
            Input = text;
        }

        const string FULLNAME_VALID_UPPER = "QWERTYUIOPASDFGHJKLMNBVCXZ ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ ÉÈẸẺẼÊẾỀỆỂỄ ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ ÚÙỤỦŨƯỨỪỰỬỮ ÍÌỊỈĨ Đ ÝỲỴỶỸ";
        const string FULLNAME_VALID_LOWER = "qwertyuiopasdfghjklmnbvcxz áàạảãâấầậẩẫăắằặẳẵ éèẹẻẽêếềệểễ óòọỏõôốồộổỗơớờợởỡ úùụủũưứừựửữ íìịỉĩ đ ýỳỵỷỹ";

        public string convertToUnicode2ascii(string s)
        {
            string stFormD = s.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            for (int ich = 0; ich < stFormD.Length; ich++)
            {
                System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[ich]);
                }
            }
            sb = sb.Replace('Đ', 'D');
            sb = sb.Replace('đ', 'd');
            return (sb.ToString().Normalize(NormalizationForm.FormD)).ToLower().Trim();
        }

        public OcrConfig Execute()
        {
            string v = this.Input.Trim(), textLower = v.ToLower(), s, t;
            string[] a1, a2;
            int pos = -1, pos2 = -1;

            //v = string.Join("|", convertToUnicode2ascii(_CITY).Split('\r').Select(x => x.Trim()).OrderBy(x => x.Length).ToArray().Reverse());

            if (textLower.Contains("đặc điểm nhận")
                || textLower.Contains("giám đốc")
                || textLower.Contains("dân tộc")
                || textLower.Contains("tôn giáo")) this.Page = 2;
            else this.Page = 1;

            if (this.Page == 1)
            {
                // Side front

                switch (Type)
                {
                    case OCR_DATA_TYPE.CMT_ID:
                        #region

                        v = v.Replace('l', '1');
                        //v = Regex.Replace(v, @"[^\d]", " ").Trim();
                        v = Regex.Replace(v, @"[^0-9:]", " ").Trim();
                        v = Regex.Replace(v, @"\s+", " ").Trim();
                        a1 = v.Split(new char[] { ':', ' ' }).Where(x => x.Length > 0).ToArray();

                        if (a1.Length > 0)
                        {
                            a2 = a1.Where(x => x.Length > 7).ToArray();
                            if (a2.Length > 0)
                            {
                                this.Result = a2[0].Trim();
                                return this;
                            }
                        }

                        this.Error = v;

                        #endregion
                        break;
                    case OCR_DATA_TYPE.CMT_FULLNAME:
                        #region
                        s = v;

                        pos = textLower.IndexOf("tên");
                        if (pos == -1) pos = textLower.IndexOf("ten");

                        pos2 = textLower.IndexOf("số");
                        if (pos == -1) pos = pos2;


                        if (pos != -1) s = s.Substring(pos + 3, v.Length - pos - 3);

                        s = s.Replace('\r', ' ').Replace('\n', ' ');
                        s = Regex.Replace(s, @"[0-9]", " ").Trim();

                        a1 = s.Split(' ').Where(x => x.Length > 0).ToArray();
                        for (int i = 0; i < a1.Length; i++)
                        {
                            for (int k = 0; k < a1[i].Length; k++)
                            {
                                if (FULLNAME_VALID_LOWER.IndexOf(a1[i][k]) != -1)
                                {
                                    s = s.Replace(a1[i], "  ");
                                    break;
                                }
                            }
                        }
                        s = s.Trim();

                        char[] ca = new char[s.Length];
                        for (int i = 0; i < s.Length; i++)
                        {
                            if (s[i] == ':' || s[i] == '.')
                            {
                                ca[i] = ' ';
                            }
                            else
                            {
                                if (FULLNAME_VALID_UPPER.IndexOf(s[i]) != -1)
                                {
                                    ca[i] = s[i];
                                }
                                else
                                {
                                    ca[i] = ' ';
                                }
                            }
                        }
                        t = new string(ca);
                        t = t.Trim();

                        a1 = t.Split(new string[] { "  " }, StringSplitOptions.None).Select(x => x.Trim()).Where(x => x.Split(' ').Length > 1).ToArray();
                        if (a1.Length > 0)
                            this.Result = a1[0];
                        else
                            this.Error = t;

                        #endregion
                        break;
                    case OCR_DATA_TYPE.CMT_BIRTHDAY:
                        #region

                        v = v.Replace('/', '-').Replace(' ', '-')
                            .Replace("-l-", "-1-").Replace("-ll-", "-11-")
                            .Replace("ll-", "-11-").Replace("l-", "1-");

                        v = Regex.Replace(v, @"[^0-9-]", " ").Trim();
                        v = Regex.Replace(v, @"\s+", " ").Trim();
                        a1 = v.Split(' ').Select((x) =>
                        {
                            string o = x.Trim();
                            if (o.Length == 0) return o;
                            o = o.Replace('-', ' ').Trim();
                            o = Regex.Replace(o, @"\s+", " ").Trim();
                            o = o.Replace(' ', '-');
                            return o;
                        }).Where(x => (x.Length >= 8 && x.Length <= 10) && x.Contains('-') && x.Split('-').Length == 3).ToArray();

                        if (a1.Length > 0)
                            this.Result = a1[0];
                        else
                            this.Error = v;

                        #endregion
                        break;
                    case OCR_DATA_TYPE.CMT_ADDRESS:
                        #region
                        s = v;

                        t = convertToUnicode2ascii(s);
                        for (int i = 0; i < ARR_CITY.Length; i++)
                        {
                            if (t.Contains(ARR_CITY[i]))
                            {
                                a1 = t.Split(new string[] { ARR_CITY[i] }, StringSplitOptions.None);
                                pos = t.Length - a1[a1.Length - 1].Length;
                                break;
                            }
                        }

                        if (pos != -1)
                        {
                            s = s.Substring(0, pos).Trim();
                            s = s.Replace('\r', ' ').Replace('\n', ' ');
                            a1 = s.Split(new string[] { ".", ":", ",", "-", "|" }, StringSplitOptions.None).Select(x => x.Trim()).Where(x => x.Length > 0).Reverse().Take(3).Reverse().ToArray();


                            if (a1[1].Contains("trú"))
                                a1[1] = a1[1].Split(new string[] { "trú" }, StringSplitOptions.None)[1].Trim();
                            else
                            {
                                if (a1[0].Contains("trú")) a1[0] = a1[0].Split(new string[] { "trú" }, StringSplitOptions.None)[1].Trim();
                                if (a1[0].Contains("quán")) a1[0] = a1[0].Split(new string[] { "quán" }, StringSplitOptions.None)[1].Trim();
                            }

                            a1 = a1.Where(x => x.Length > 0 && (x.Split(' ').Length > 1 && x.Split(' ').Length < 5)).Distinct().ToArray();

                            t = string.Join(", ", a1);

                            this.Result = t;
                            return this;
                        }
                        //this.Error = s;

                        #endregion
                        break;
                }
            }
            else
            {
                // Side back
            }

            return this;
        }
    }

    public class CMT
    {
        public string id = "N/A";
        public string address = "N/A";
        public string fullname = "N/A";
        public string birthday = "N/A";
        public string expiry = "N/A";
        public string file = "N/A";
        public string error = "";
        public string text = "";
        public int page = 1;

        //public string gender = "N/A";
        //public string ethnicity = "N/A";
        //public string issue_by = "N/A";
        //public string issue_date = "N/A";
        //public string religion = "N/A";

        public bool ok = false;
    }


    public enum STATE_GOO_SERVICE
    {
        NONE,
        GOO_AUTHEN_PROCESSING,
        GOO_AUTHEN_SUCCESS,
        GOO_AUTHEN_FAIL_MISS_KEY,
        GOO_AUTHEN_FAIL_INVALID_KEY,
    }

    public enum STATE_OCR
    {
        NONE,
        OCR_IS_BUSY,
        OCR_FAIL_AUTHEN,
        OCR_FAIL_MISS_FILE,
        OCR_FAIL_MISS_QUERY_STRING,
        OCR_FAIL_READ_FILE,
        OCR_FAIL_DOWNLOAD_FILE,
        OCR_FAIL_ROTATE_IMAGE,
        OCR_FAIL_THROW_ERROR,
        OCR_FAIL_ENGINE_ERROR,
        OCR_SUCCESS
    }

    public enum SIDE_IMAGE
    {
        FRONT,
        BACK
    }

    public class OcrImageInfo
    {
        public bool WriteToFile { get; set; }
        public bool IsUrl { get; set; }
        public bool DownloadSuccess { get; set; }
        public long TimeStart { get; set; }
        public long TimeComplete { get; set; }

        public SIDE_IMAGE SideImage { get; set; }
        public string TextError { get; set; }
        public string TextResult { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public STATE_OCR StateOcr { get; set; }

        public string app_getJsonResult()
        {
            bool ok = this.StateOcr == STATE_OCR.OCR_SUCCESS;
            var ocr = new CMT();

            if (ok)
            {
                try
                {
                    string xText = this.TextResult;

                    var id_ = new OcrConfig(xText, OCR_DATA_TYPE.CMT_ID).Execute();
                    var name_ = new OcrConfig(xText, OCR_DATA_TYPE.CMT_FULLNAME).Execute();
                    var birthday_ = new OcrConfig(xText, OCR_DATA_TYPE.CMT_BIRTHDAY).Execute();
                    var address_ = new OcrConfig(xText, OCR_DATA_TYPE.CMT_ADDRESS).Execute();

                    StringBuilder bi = new StringBuilder();
                    if (!id_.Success) bi.Append(id_.Error + Environment.NewLine);
                    if (!name_.Success) bi.Append(name_.Error + Environment.NewLine);
                    if (!birthday_.Success) bi.Append(birthday_.Error + Environment.NewLine); ;
                    if (!address_.Success) bi.Append(address_.Error + Environment.NewLine);

                    ocr.page = id_.Page;
                    ocr.id = id_.Result;
                    ocr.fullname = name_.Result;
                    ocr.birthday = birthday_.Result;
                    ocr.address = address_.Result;

                    ocr.file = this.FileName;
                    ocr.text = xText;

                    //ocr.error = id_.Error;
                    //ocr.error = name_.Error;
                    //ocr.error = birthday_.Error;
                    ocr.error = address_.Error;
                }
                catch { }
            }


            string json = JsonConvert.SerializeObject(new
            {
                Ok = ok,
                //ServiceState = app != null ? app.StateGooService.ToString() : STATE_GOO_SERVICE.NONE.ToString(),
                State = this.StateOcr.ToString(),
                Request = new
                {
                    File = this.FileName,
                    Url = this.Url,
                    Side = this.SideImage.ToString()
                },
                Result = new
                {
                    Text = this.TextResult,
                    //Item = ok ? new CMT(true, this.TextResult) : new CMT(this.TextError)
                    Item = ocr
                },
                Error = this.TextError,
                TimeStart = this.TimeStart
            }, Formatting.Indented);

            long timeComplete = long.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));
            json = json.Substring(0, json.Length - 1) + Environment.NewLine +
                @", ""TimeComplete"": " + timeComplete + "}";

            return json;
        }
    }

    public interface IApp
    {
        string PATH_OCR_IMAGE { get; }
        STATE_GOO_SERVICE StateGooService { get; set; }

        //string app_getJsonResult(OcrImageInfo ocr);
        string app_getJsonToken();
        string app_getState();

        string setTitleMessage(string message = "");
        void writeLogMessage(string message = "");

        OcrImageInfo goo_ocr_uploadFile(OcrImageInfo ocr);
    }
}
