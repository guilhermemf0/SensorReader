using SensorReader.Models;
using SensorReader.Output;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace SensorReader.Tests
{
    public class FormatterTests
    {
        [Fact]
        public void PlainTextFormatter_FormatsSimpleCpuSensor_Correctly()
        {
            var report = new HardwareReport();
            var cpu = new CpuInfo { Name = "Test CPU" };
            cpu.Sensors.Add(new Sensor
            {
                Name = "CPU Total",
                Type = SensorType.Load,
                Value = 50.5f
            });
            report.Cpus.Add(cpu);

            var formatter = new PlainTextOutputFormatter();

            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            string expectedOutput = "CPU_LOAD_CPU_TOTAL:50.5;";


            formatter.Write(report);
            var actualOutput = stringWriter.ToString();

            Assert.Equal(expectedOutput, actualOutput);
        }

        [Fact]
        public void PlainTextFormatter_SanitizesComplexKeys_Correctly()
        {
            var report = new HardwareReport();
            var mb = new MotherboardInfo();
            mb.Sensors.Add(new Sensor
            {
                Name = "VRM MOS 1/2",
                Type = SensorType.Temperature,
                Value = 75.0f
            });
            report.Motherboard = mb;

            var formatter = new PlainTextOutputFormatter();
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            string expectedOutput = "MB_TEMPERATURE_VRM_MOS_1_2:75.0;";

            formatter.Write(report);
            var actualOutput = stringWriter.ToString();

            Assert.Equal(expectedOutput, actualOutput);
        }

        [Fact]
        public void JsonFormatter_HandlesSpecialFloatValues_WithoutCrashing()
        {
            var report = new HardwareReport();
            var gpu = new GpuInfo { Name = "Test GPU" };
            gpu.Sensors.Add(new Sensor
            {
                Name = "GPU Power",
                Type = SensorType.Power,
                Value = float.NaN
            });
            report.Gpus.Add(gpu);

            var formatter = new JsonOutputFormatter();
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            Exception? caughtException = null;
            string actualOutput = "";

            try
            {
                formatter.Write(report);
                actualOutput = stringWriter.ToString();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            Assert.Null(caughtException);

            Assert.Contains("\"Value\": \"NaN\"", actualOutput);

            var jsonDoc = JsonDocument.Parse(actualOutput);
            Assert.NotNull(jsonDoc);
        }
    }
}
