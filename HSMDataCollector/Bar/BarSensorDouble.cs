﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using HSMDataCollector.Core;
using HSMDataCollector.PublicInterface;
using HSMSensorDataObjects;
using HSMSensorDataObjects.BarData;
using HSMSensorDataObjects.FullDataObject;

namespace HSMDataCollector.Bar
{
    public class BarSensorDouble : BarSensorBase, IDoubleBarSensor
    {
        private readonly SortedSet<double> ValuesList;
        public BarSensorDouble(string path, string productKey, string serverAddress, IValuesQueue queue, int collectPeriod = 300000,
            int smallPeriod = 15000)
            : this(path, productKey, $"{serverAddress}/doubleBar", queue, TimeSpan.FromMilliseconds(collectPeriod),
                TimeSpan.FromMilliseconds(smallPeriod))
        {
            
        }

        public BarSensorDouble(string path, string productKey, string serverAddress, IValuesQueue queue,
            TimeSpan collectPeriod,
            TimeSpan smallPeriod) : base(path, productKey, $"{serverAddress}/doubleBar", queue, collectPeriod,
            smallPeriod)
        {
            ValuesList = new SortedSet<double>();
        }

        protected override void SmallTimerTick(object state)
        {
            DoubleBarSensorValue dataObject;
            try
            {
                dataObject = GetPartialDataObject();
            }
            catch (Exception e)
            {
                return;
            }
            CommonSensorValue commonValue = ToCommonSensorValue(dataObject);
            SendData(commonValue);
        }

        public override CommonSensorValue GetLastValue()
        {
            try
            {
                DoubleBarSensorValue dataObject = GetDataObject();
                CommonSensorValue commonValue = ToCommonSensorValue(dataObject);
                return commonValue;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        protected override void SendDataTimer(object state)
        {
            DoubleBarSensorValue dataObject = GetDataObject();
            CommonSensorValue commonValue = ToCommonSensorValue(dataObject);
            SendData(commonValue);
        }

        public void AddValue(double value)
        {
            lock (_syncObject)
            {
                ValuesList.Add(value);
            }
        }

        private CommonSensorValue ToCommonSensorValue(DoubleBarSensorValue data)
        {
            CommonSensorValue result = new CommonSensorValue();
            string serializedValue = GetStringData(data);
            result.TypedValue = serializedValue;
            result.SensorType = SensorType.DoubleBarSensor;
            return result;
        }
        private DoubleBarSensorValue GetPartialDataObject()
        {
            DoubleBarSensorValue result = new DoubleBarSensorValue();
            List<double> currentValues;
            lock (_syncObject)
            {
                currentValues = new List<double>(ValuesList);

                result.StartTime = barStart;
            }

            FillNumericData(result, currentValues);
            FillCommonData(result);
            result.EndTime = DateTime.MinValue;

            return result;
        }
        private DoubleBarSensorValue GetDataObject()
        {
            DoubleBarSensorValue result = new DoubleBarSensorValue();
            List<double> collected;
            lock (_syncObject)
            {
                collected = new List<double>(ValuesList);
                ValuesList.Clear();

                //New bar starts right after the previous one ends
                result.StartTime = barStart;
                result.EndTime = DateTime.Now;
                barStart = DateTime.Now;
            }

            FillNumericData(result, collected);

            FillCommonData(result);
            return result;
        }

        private void FillNumericData(DoubleBarSensorValue value, List<double> values)
        {
            value.Max = values.Last();
            value.Min = values.First();
            value.Count = values.Count;
            value.Mean = CountMean(values);
            value.Percentiles.Add(new PercentileValueDouble(GetPercentile(values, 0.25), 0.25));
            value.Percentiles.Add(new PercentileValueDouble(GetPercentile(values, 0.5), 0.5));
            value.Percentiles.Add(new PercentileValueDouble(GetPercentile(values, 0.75), 0.75));
        }

        private void FillCommonData(DoubleBarSensorValue value)
        {
            value.Key = ProductKey;
            value.Path = Path;
            value.Time = DateTime.Now;
        }
        protected override string GetStringData(SensorValueBase data)
        {
            try
            {
                DoubleBarSensorValue typedData = (DoubleBarSensorValue)data;
                return JsonSerializer.Serialize(typedData);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return string.Empty;
            }
        }

        protected override byte[] GetBytesData(SensorValueBase data)
        {
            try
            {
                DoubleBarSensorValue typedData = (DoubleBarSensorValue)data;
                string convertedString = JsonSerializer.Serialize(typedData);
                return Encoding.UTF8.GetBytes(convertedString);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new byte[1];
            }
            
        }

        private double CountMean(List<double> values)
        {
            double sum = values.Sum();
            double mean = 0.0;
            try
            {
                mean = sum / values.Count;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return mean;
        }
    }
}
