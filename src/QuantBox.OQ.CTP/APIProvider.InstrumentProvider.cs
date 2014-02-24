using SmartQuant.FIX;
using SmartQuant.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

#if CTP
using QuantBox.CSharp2CTP;
using QuantBox.Helper.CTP;

namespace QuantBox.OQ.CTP
#elif CTPZQ
using QuantBox.CSharp2CTPZQ;
using QuantBox.Helper.CTPZQ;

namespace QuantBox.OQ.CTPZQ
#endif
{
    public partial class APIProvider : IInstrumentProvider
    {
        public event SecurityDefinitionEventHandler SecurityDefinition;

        public void SendSecurityDefinitionRequest(FIXSecurityDefinitionRequest request)
        {
            lock (this)
            {
                if (!_bTdConnected)
                {
                    EmitError(-1, -1, "交易没有连接，无法获取合约列表");
                    tdlog.Error("交易没有连接，无法获取合约列表");
                    return;
                }

                string symbol = request.ContainsField(EFIXField.Symbol) ? request.Symbol : null;
                string securityType = request.ContainsField(EFIXField.SecurityType) ? request.SecurityType : null;
                string securityExchange = request.ContainsField(EFIXField.SecurityExchange) ? request.SecurityExchange : null;

                #region 过滤
                List<CThostFtdcInstrumentField> list = new List<CThostFtdcInstrumentField>();
                foreach (CThostFtdcInstrumentField inst in _dictInstruments.Values)
                {
                    int flag = 0;
                    if (null == symbol)
                    {
                        ++flag;
                    }
                    else if (inst.InstrumentID.ToUpper().StartsWith(symbol.ToUpper()))
                    {
                        ++flag;
                    }

                    if (null == securityExchange)
                    {
                        ++flag;
                    }
                    else if (inst.ExchangeID.ToUpper().StartsWith(securityExchange.ToUpper()))
                    {
                        ++flag;
                    }

                    if (null == securityType)
                    {
                        ++flag;
                    }
                    else
                    {
                        if (securityType == GetSecurityType(inst))
                        {
                            ++flag;
                        }
                    }
                    
                    if (3==flag)
                    {
                        list.Add(inst);
                    }
                }
                #endregion

                list.Sort(SortCThostFtdcInstrumentField);

                //如果查出的数据为0，应当想法立即返回
                if (0==list.Count)
                {
                    FIXSecurityDefinition definition = new FIXSecurityDefinition
                    {
                        SecurityReqID = request.SecurityReqID,
                        SecurityResponseID = request.SecurityReqID,
                        SecurityResponseType = request.SecurityRequestType,
                        TotNoRelatedSym = 1//有个除0错误的问题
                    };
                    if (SecurityDefinition != null)
                    {
                        SecurityDefinition(this, new SecurityDefinitionEventArgs(definition));
                    }
                }

                foreach (CThostFtdcInstrumentField inst in list)
                {
                    FIXSecurityDefinition definition = new FIXSecurityDefinition
                    {
                        SecurityReqID = request.SecurityReqID,
                        //SecurityResponseID = request.SecurityReqID,
                        SecurityResponseType = request.SecurityRequestType,
                        TotNoRelatedSym = list.Count
                    };

                    {
                        string securityType2 = GetSecurityType(inst);
                        definition.AddField(EFIXField.SecurityType, securityType2);
                    }
                    {
                        double x = inst.PriceTick;
                        if (x > 0.00001)
                        {
                            int i = 0;
                            for (; x - (int)x != 0; ++i)
                            {
                                x = x * 10;
                            }
                            definition.AddField(EFIXField.PriceDisplay, string.Format("F{0}", i));
                            definition.AddField(EFIXField.TickSize, inst.PriceTick);
                        }
                    }
#if CTP
                    definition.AddField(EFIXField.Symbol, inst.InstrumentID);
#elif CTPZQ
                    definition.AddField(EFIXField.Symbol, GetYahooSymbol(inst.InstrumentID, inst.ExchangeID));
#endif
                    definition.AddField(EFIXField.SecurityExchange, inst.ExchangeID);
                    definition.AddField(EFIXField.Currency, "CNY");//Currency.CNY
                    definition.AddField(EFIXField.SecurityDesc, inst.InstrumentName);
                    definition.AddField(EFIXField.Factor, (double)inst.VolumeMultiple);

                    if(inst.ProductClass == TThostFtdcProductClassType.Futures||inst.ProductClass == TThostFtdcProductClassType.Options)
                    {
                        try
                        {
                            definition.AddField(EFIXField.MaturityDate, DateTime.ParseExact(inst.ExpireDate, "yyyyMMdd", CultureInfo.InvariantCulture));
                        }
                        catch (Exception ex)
                        {
                            tdlog.Warn("合约:{0},字段内容:{1},{2}", inst.InstrumentID, inst.ExpireDate, ex.Message);
                        }

                        if (inst.ProductClass == TThostFtdcProductClassType.Options)
                        {
                            // 支持中金所，大商所，郑商所
                            var match = Regex.Match(inst.InstrumentID, @"(\d+)(-?)([CP])(-?)(\d+)");
                            if (match.Success)
                            {
                                definition.AddField(EFIXField.PutOrCall, match.Groups[3].Value == "C" ? FIXPutOrCall.Call : FIXPutOrCall.Put);
                                definition.AddField(EFIXField.StrikePrice, double.Parse(match.Groups[5].Value));
                            }
                        }
                    }

                    FIXSecurityAltIDGroup group = new FIXSecurityAltIDGroup();
                    group.SecurityAltID = inst.InstrumentID;
                    group.SecurityAltExchange = inst.ExchangeID;
                    group.SecurityAltIDSource = this.Name;

                    definition.AddGroup(group);
                    
                    //还得补全内容

                    if (SecurityDefinition != null)
                    {
                        SecurityDefinition(this, new SecurityDefinitionEventArgs(definition));
                    }
                }
            }
        }

        private static int SortCThostFtdcInstrumentField(CThostFtdcInstrumentField a1, CThostFtdcInstrumentField a2)
        {
            return a1.InstrumentID.CompareTo(a2.InstrumentID);
        }
        #region 证券接口

        /*
         * 上海证券交易所证券代码分配规则
         * http://www.docin.com/p-417422186.html
         * 
         * http://wenku.baidu.com/view/f2e9ddf77c1cfad6195fa706.html
         */
        private string GetSecurityType(CThostFtdcInstrumentField inst)
        {
            string securityType = FIXSecurityType.CommonStock;

            try
            {
                switch (inst.ProductClass)
                {
                    case TThostFtdcProductClassType.Futures:
                        securityType = FIXSecurityType.Future;
                        break;
                    case TThostFtdcProductClassType.Combination:
                        securityType = FIXSecurityType.MultiLegInstrument;//此处是否理解上有不同
                        break;
                    case TThostFtdcProductClassType.Options:
                        securityType = FIXSecurityType.FutureOption;
                        break;
#if CTPZQ
                case TThostFtdcProductClassType.StockA:
                case TThostFtdcProductClassType.StockB:
                    securityType = GetSecurityTypeStock(inst.ProductID, inst.InstrumentID);
                    break;
                case TThostFtdcProductClassType.ETF:
                case TThostFtdcProductClassType.ETFPurRed:
                    securityType = GetSecurityTypeETF(inst.ProductID, inst.InstrumentID);
                    break;
#endif
                    default:
                        securityType = FIXSecurityType.CommonStock;
                        break;
                }
                return securityType;
            }
            catch(Exception ex)
            {
                tdlog.Warn("合约:{0},字段内容:{1}", inst.InstrumentID, ex.Message);
            }

            return securityType;
        }

        /*
        从CTPZQ中遍历出来的所有ID
        GC 6 090002
        SHETF 8 500001
        SHA 6 600000
        SZA 6 000001
        SZBONDS 6 100213
        RC 6 131800
        SZETF 8 150001*/
        private string GetSecurityTypeStock(string ProductID, string InstrumentID)
        {
            string securityType = FIXSecurityType.CommonStock;
            switch (ProductID)
            {
                case "SHA":
                case "SZA":
                    securityType = FIXSecurityType.CommonStock;
                    break;
                case "SHBONDS":
                    {
                        int i = Convert.ToInt32(InstrumentID.Substring(0, 3));
                        if (i == 0)
                        {
                            securityType = FIXSecurityType.Index;
                        }
                        else if (i < 700)
                        {
                            securityType = FIXSecurityType.USTreasuryBond;
                        }
                        else if (i < 800)
                        {
                            securityType = FIXSecurityType.CommonStock;
                        }
                        else
                        {
                            securityType = FIXSecurityType.USTreasuryBond;
                        }
                    }
                    break;
                case "SZBONDS":
                    {
                        int i = Convert.ToInt32(InstrumentID.Substring(0, 2));
                        if (i == 39)
                        {
                            securityType = FIXSecurityType.Index;
                        }
                        else
                        {
                            securityType = FIXSecurityType.USTreasuryBond;
                        }
                    }
                    break;
                case "GC":
                case "RC":
                    securityType = FIXSecurityType.USTreasuryBond;
                    break;
                case "SHETF":
                case "SZETF":
                    securityType = FIXSecurityType.ExchangeTradedFund;
                    break;
                case "SHRATIONED":
                case "SZRATIONED":
                case "SZCYB":
                    securityType = FIXSecurityType.CommonStock;
                    break;
                default:
                    securityType = FIXSecurityType.CommonStock;
                    break;
            }
            return securityType;
        }

        private string GetSecurityTypeETF(string ProductID, string InstrumentID)
        {
            string securityType = FIXSecurityType.ExchangeTradedFund;
            switch (ProductID)
            {
                case "SHA":
                    securityType = FIXSecurityType.CommonStock;
                    break;
                case "SHETF":
                    securityType = FIXSecurityType.ExchangeTradedFund;
                    break;
                case "SZETF":
                    securityType = FIXSecurityType.ExchangeTradedFund;
                    break;
                case "SZA":
                    {
                        int i = Convert.ToInt32(InstrumentID.Substring(0, 2));
                        if (i < 10)
                        {
                            securityType = FIXSecurityType.CommonStock;
                        }
                        else if (i < 15)
                        {
                            securityType = FIXSecurityType.USTreasuryBond;
                        }
                        else if (i < 20)
                        {
                            securityType = FIXSecurityType.ExchangeTradedFund;
                        }
                        else if (i < 30)
                        {
                            securityType = FIXSecurityType.CommonStock;
                        }
                        else if (i < 39)
                        {
                            securityType = FIXSecurityType.CommonStock;
                        }
                        else if (i == 39)
                        {
                            securityType = FIXSecurityType.Index;
                        }
                        else
                        {
                            securityType = FIXSecurityType.CommonStock;
                        }
                    }
                    break;
            }
            return securityType;
        }

        private string GetYahooSymbol(string InstrumentID, string ExchangeID)
        {
            if (InstrumentID.Length >= 6 && ExchangeID.Length >= 2)
            {
                return string.Format("{0}.{1}", InstrumentID.Substring(0,6), ExchangeID.Substring(0, 2));
            }
            else
            {
                // 没有交易所信息时的容错处理
                string altSymbol;
                if (_dictInstruments2.TryGetValue(InstrumentID, out altSymbol))
                {
                    return altSymbol;
                }

                return string.Format("{0}.{1}", InstrumentID, ExchangeID);
            }
        }

        private string GetApiSymbol(string Symbol)
        {
            var match = Regex.Match(Symbol, @"(\d+)\.(\w+)");
            if (match.Success)
            {
                var code = match.Groups[1].Value;
                return code;
            }
            return Symbol;
        }

        private string GetApiExchange(string Symbol)
        {
            var match = Regex.Match(Symbol, @"(\d+)\.(\w+)");
            if (match.Success)
            {
                var code = match.Groups[2].Value;
                return code;
            }
            return Symbol;
        }
        #endregion
    }
}
