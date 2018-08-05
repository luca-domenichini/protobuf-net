﻿#pragma warning disable RCS1213

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.IO;

namespace Benchmark
{
    public static class Program
    {
        [ProtoContract]
        private class Foo
        {
            [ProtoMember(1)]
            public Bar Bar { get; set; }
        }
        [ProtoContract]
        private class Bar
        {
            [ProtoMember(1)]
            public int Id { get; set; }
        }

        private static void Main()
        {
            //ParseNWind();
            //TestWriteRead();
            Console.WriteLine(BenchmarkRunner.Run<Benchmarks>());
        }
        private static void ParseNWind()
        {
            var b = new Benchmarks();
            b.Setup();
            using (var reader = b.ReadROS())
            {
                // var dal = b.Model.Deserialize(reader, null, typeof(DAL.Database));

                /*
0A = field 1, type String           == Database.Orders
A1-01 = length 161
payload = ...

08 = field 1, type Variant          == Order.OrderID
88-50 = 10248 (raw) or 5124 (zigzag)

12 = field 2, type String           == Order.CustomerID
05 = length 5
                 */
                int field = reader.ReadFieldHeader();
                Console.WriteLine($"field {field}, {reader.WireType}");
                var tok = ProtoReader.StartSubItem(reader);
                Console.WriteLine(tok.ToString());
                field = reader.ReadFieldHeader();
                Console.WriteLine($"field {field}, {reader.WireType}");
                Console.WriteLine($"OrderID = {reader.ReadInt32()}");
                field = reader.ReadFieldHeader();
                Console.WriteLine($"field {field}, {reader.WireType}");
                Console.WriteLine($"CustomerID = {reader.ReadString()}");
                while (reader.ReadFieldHeader() != 0)
                    reader.SkipField();
                ProtoReader.EndSubItem(tok, reader);
            }
        }
        private static void TestWriteRead()
        {
            var ms = new MemoryStream();
            Serializer.Serialize(ms, new Foo { Bar = new Bar { Id = 42 } });
            using (var reader = ProtoReader.Create(
                new ReadOnlySequence<byte>(ms.GetBuffer(), 0, (int)ms.Length),
                RuntimeTypeModel.Default))
            {
                while (reader.ReadFieldHeader() > 0)
                {
                    Console.WriteLine($"pos {reader.Position}, field {reader.FieldNumber}, type {reader.WireType}");
                    switch (reader.FieldNumber)
                    {
                        case 1:
                            var tok = ProtoReader.StartSubItem(reader);
                            Console.WriteLine(tok);
                            while (reader.ReadFieldHeader() > 0)
                            {
                                Console.WriteLine($"\tpos {reader.Position}, field {reader.FieldNumber}, type {reader.WireType}");
                                switch (reader.FieldNumber)
                                {
                                    case 1:
                                        Console.WriteLine($"\tId={reader.ReadInt32()}");
                                        break;
                                    default:
                                        throw new InvalidOperationException("wasn't expecting that");
                                }
                            }
                            ProtoReader.EndSubItem(tok, reader);
                            break;
                        default:
                            throw new InvalidOperationException("wasn't expecting that");
                    }
                }
            }
            using (var reader = ProtoReader.Create(
                new ReadOnlySequence<byte>(ms.GetBuffer(), 0, (int)ms.Length),
                RuntimeTypeModel.Default))
            {
                var foo = (Foo)RuntimeTypeModel.Default.Deserialize(reader, null, typeof(Foo));
                Console.WriteLine(foo.Bar.Id);
            }
        }
    }

    [ClrJob, CoreJob, MemoryDiagnoser]
    public class Benchmarks
    {
        private MemoryStream _ms;
        private ReadOnlySequence<byte> _ros;
        public ProtoReader ReadMS() => ProtoReader.Create(_ms, Model);
        public ProtoReader ReadROS() => ProtoReader.Create(_ros, Model);

        public TypeModel Model => RuntimeTypeModel.Default;

        [GlobalSetup]
        public void Setup()
        {
            var data = File.ReadAllBytes("nwind.proto.bin");
            _ms = new MemoryStream(data);
            _ros = new ReadOnlySequence<byte>(data);
        }

        [Benchmark(Baseline = true)]
        public void MemoryStream()
        {
            _ms.Position = 0;
            using (var reader = ReadMS())
            {
                var dal = Model.Deserialize(reader, null, typeof(DAL.Database));
                GC.KeepAlive(dal);
            }
        }
        [Benchmark(Description = "ReadOnlySequence<byte>")]
        public void ReadOnlySequence()
        {
            using (var reader = ReadROS())
            {
                var dal = Model.Deserialize(reader, null, typeof(DAL.Database));
                GC.KeepAlive(dal);
            }
        }
    }
}
