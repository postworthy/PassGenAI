using Accord.Statistics.Filters;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using PassGenAI.HMM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PassGenAI.Core;
using OpenCL.Net;

namespace PassGenAI.Keyboard
{
    public class KeyboardWalks
    {
        private static string[][] keyboard = new string[][] {
            new[] { "`~","1!","2@","3#","4$","5%","6^","7&","8*","9(","0)","-_","=+", "" },
            new[] { "","qQ","wW","eE","rR","tT","yY","uU","iI","oO","pP","[{","]}", "\\|" },
            new[] { "","aA","sS","dD","fF","gG","hH","jJ","kK","lL",";:","'\"","", "" },
            new[] { "","zZ","xX","cC","vV","bB","nN","mM",",<",".>","/?","","", "" },
            //new[] { "  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ","  ", "  " }
        };

        private static (int x, int y)[][] complexMoveMatrix = new[] {
            new (int x, int y)[]{ (-1, -1), (0, -1), (1, -1)},
            new (int x, int y)[]{ (-1, 0), (0, 0), (1, 0)},
            new (int x, int y)[]{ (-1, 1), (0, 1), (1, 1)},
        };

        private static (int x, int y)[][] simpleMoveMatrix = new[] {
            new (int x, int y)[]{ (0, 0), (0, -1), (0, 0)},
            new (int x, int y)[]{ (-1, 0), (0, 0), (1, 0)},
            new (int x, int y)[]{ (0, 0), (0, 1), (0, 0)},
        };

        public static IEnumerable<int> ChainWalker(int x, long skip)
        {
            var source = ChainWalker(x);
            while (skip > int.MaxValue)
            {
                source = source.Skip(int.MaxValue);
                skip -= int.MaxValue;
            }
            source = source.Skip((int)skip);
            return source;
        }

        static IEnumerable<int> ChainWalker(int x)
        {
            while (true)
            {
                foreach (int i in Enumerable.Range(0, x))
                {
                    yield return i;
                }
            }
        }

        public static IEnumerable<string> Walk(int length, bool simple = true)
        {
            var ngrams = new List<string[]>();

            var moves = simple ? simpleMoveMatrix.SelectMany(m => m).ToList() : complexMoveMatrix.SelectMany(m => m).ToList();
            for (int y = 0; y < keyboard.Length; y++)
            {
                for (int x = 0; x < keyboard[y].Length; x++)
                {
                    moves.ForEach(m =>
                    {
                        if (m.x + x >= 0 && m.y + y >= 0 && m.x + x <= keyboard[0].Length - 1 && m.y + y <= keyboard.Length - 1 && !(m.x == 0 && m.y == 0))
                        {
                            var part1 = keyboard[y][x];
                            var part2 = keyboard[y + m.y][m.x + x];
                            if (!string.IsNullOrEmpty(part1) && !string.IsNullOrEmpty(part2))
                            {
                                ngrams.Add(new[] { part1[0].ToString(), part2[0].ToString() });
                                ngrams.Add(new[] { part1[0].ToString(), part2[1].ToString() });
                                ngrams.Add(new[] { part1[1].ToString(), part2[1].ToString() });
                                ngrams.Add(new[] { part1[1].ToString(), part2[0].ToString() });
                            }
                        }
                    });
                }
            }

            var ngramGroups = ngrams.GroupBy(x => x[0]).ToList();

            var ngramLookup = new Dictionary<string, List<string>>();

            ngramGroups.ForEach(ng => ngramLookup.Add(ng.Key, ng.Select(x => x[1]).ToList()));
            ngramGroups = null;

            var queue = new System.Collections.Concurrent.ConcurrentQueue<List<string>>();

            var t = Task.Factory.StartNew(() =>
            {
                ngramLookup.Keys.AsParallel().ForAll(key =>
                {
                    var results = ngramLookup[key.ToString()].Select(x => key + x).ToList();
                    while (results.First().Length < length)
                    {
                        var nextResults = new List<string>(results.Count * ngramLookup.Keys.Count);
                        foreach (var r in results)
                        {
                            nextResults.AddRange(ngramLookup[r.Last().ToString()].Select(x => r + x));
                        }
                        results = nextResults;
                    }

                    queue.Enqueue(results);
                });
            });

            while (t.Status != TaskStatus.Running) ; //Wait for it to get started

            do
            {
                while (queue.TryDequeue(out var items))
                {
                    foreach (var item in items)
                    {
                        yield return item;
                    }
                }
            } while (t.Status == TaskStatus.Running);
        }

        public static IEnumerable<string> OclWalk(int length, bool simple = true)
        {
            var ngrams = new List<char[]>();

            var moves = simple ? simpleMoveMatrix.SelectMany(m => m).ToList() : complexMoveMatrix.SelectMany(m => m).ToList();
            for (int y = 0; y < keyboard.Length; y++)
            {
                for (int x = 0; x < keyboard[y].Length; x++)
                {
                    moves.ForEach(m =>
                    {
                        if (m.x + x >= 0 && m.y + y >= 0 && m.x + x <= keyboard[0].Length - 1 && m.y + y <= keyboard.Length - 1 && !(m.x == 0 && m.y == 0))
                        {
                            var part1 = keyboard[y][x];
                            var part2 = keyboard[y + m.y][m.x + x];
                            if (!string.IsNullOrEmpty(part1) && !string.IsNullOrEmpty(part2))
                            {
                                ngrams.Add(new[] { part1[0], part2[0] });
                                ngrams.Add(new[] { part1[0], part2[1] });
                                ngrams.Add(new[] { part1[1], part2[1] });
                                ngrams.Add(new[] { part1[1], part2[0] });
                            }
                        }
                    });
                }
            }

            var tblNgram = ngrams.GroupBy(x => x[0]).Select(x =>
            {
                var items = x.Select(y => y[1]).ToList();
                items.Insert(0, x.Key);
                return items.ToArray();
            }).ToArray();

            var longestChain = tblNgram.Max(x => x.Count()) + 1;


            tblNgram = tblNgram.Select(x =>
            {
                var n = new char[longestChain];
                Array.Copy(x, n, x.Length);
                return n;
            }).ToArray();

            var queue = new System.Collections.Concurrent.ConcurrentQueue<(char[] Data, int ItemLength)>();

            var t = ExecuteOnDevice(tblNgram, length, x => queue.Enqueue(x));

            while (t.Status != TaskStatus.Running) ; //Wait for it to get started

            do
            {
                while (queue.TryDequeue(out var item))
                {
                    for (int i = 0; i < item.Data.Length / item.ItemLength; i++)
                    {
                        var str = "";
                        for (int j = 0; j < item.ItemLength; j++)
                        {
                            str += item.Data[i * item.ItemLength + j];
                        }
                        yield return str;
                    }
                }
            } while (t.Status == TaskStatus.Running);
        }

        private static Task ExecuteOnDevice(char[][] ngrams, int length, Action<(char[] Data, int ItemLength)> returnData)
        {
            var t = Task.Factory.StartNew(() =>
            {
                // Create a compute device, create a context and a command queue
                Event event0; ErrorCode err;
                Platform[] platforms = Cl.GetPlatformIDs(out err);
                Device[] devices = Cl.GetDeviceIDs(platforms[0], DeviceType.Gpu, out err);
                Device device = devices[0]; //cl_device_id device;
                Context context = Cl.CreateContext(null, 1, devices, null, IntPtr.Zero, out err);
                CommandQueue cmdQueue = Cl.CreateCommandQueue(context, device, CommandQueueProperties.None, out err);


                string programSource = @"
                __kernel void
                walk(__global char* tblNgram, __global char* globalList, long totalElements, int ngi, int chainCount, int longestChain, int length)
                {
	                int groupItemIndex = get_global_id(0);
	                char previousChar;
	                for (int pass = 0; pass < length; pass++)
	                {
		                int groupSize = (int)(totalElements / pow((double)longestChain, (double)pass));
		                int groupCount = totalElements / groupSize;
		                int group = groupItemIndex / groupSize;
		                int globalOffset = (groupItemIndex * length) + pass;
		                int ngl = 0;
		                if (pass != 0)
		                {
			                for (int i = 0; i < chainCount; i++)
			                {
				                if (tblNgram[i*longestChain] == previousChar)
					                ngl = i;
			                }
		                }
		                int index = group < longestChain ? group : (int)((((group * 1.0) / longestChain) - trunc((group * 1.0) / longestChain)) * longestChain);
		                globalList[globalOffset] = tblNgram[((pass == 0 ? ngi : ngl)*longestChain) + (pass == 0 ? 0 : index)];
                        previousChar = tblNgram[((pass == 0 ? ngi : ngl)*longestChain) + (pass == 0 ? 0 : index)];
	                }
                };";
                var program = Cl.CreateProgramWithSource(context, 1, new[] { programSource }, null, out err);
                Cl.BuildProgram(program, 0, null, string.Empty, null, IntPtr.Zero);  //"-cl-mad-enable"

                if (Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Status, out err).CastTo<BuildStatus>() != BuildStatus.Success)
                {
                    if (err != ErrorCode.Success)
                        Console.WriteLine("ERROR: " + "Cl.GetProgramBuildInfo" + " (" + err.ToString() + ")");
                    Console.WriteLine("Cl.GetProgramBuildInfo != Success");
                    Console.WriteLine(Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out err));
                }


                Kernel kernel = Cl.CreateKernel(program, "walk", out err);

                var ngramChains = ngrams.SelectMany(x => x).ToArray();
                var longestChain = ngrams.Max(x => x.Count()) + 1;

                Mem memChains = (Mem)Cl.CreateBuffer(context, MemFlags.ReadOnly, OpenCL.Net.TypeSize<byte>.SizeInt * ngramChains.Length, out err);

                err = Cl.EnqueueWriteBuffer(cmdQueue, (IMem)memChains, Bool.True, IntPtr.Zero, new IntPtr(OpenCL.Net.TypeSize<byte>.SizeInt * ngramChains.Length), Array.ConvertAll(ngramChains, c => Convert.ToByte(c)), 0, null, out event0);
                if (ErrorCode.Success != err)
                {
                    Console.WriteLine("Error calling EnqueueNDRangeKernel: {0}", err.ToString());
                    throw new Exception(err.ToString());
                }

                IntPtr notused;
                InfoBuffer local = new InfoBuffer(new IntPtr(4));
                Cl.GetKernelWorkGroupInfo(kernel, device, KernelWorkGroupInfo.WorkGroupSize, new IntPtr(sizeof(int)), local, out notused);

                ngrams.ToList().ForEach(ng =>
                {
                    var ngi = ngrams.ToList().IndexOf(ng);
                    var totalElements = (long)Math.Pow(longestChain, length);
                    var globalList = new char[totalElements * length];

                    
                    Mem memList = (Mem)Cl.CreateBuffer(context, MemFlags.WriteOnly, OpenCL.Net.TypeSize<byte>.SizeInt * globalList.Length, out err);
                    
                    Cl.SetKernelArg(kernel, 0, memChains);
                    Cl.SetKernelArg(kernel, 1, memList);
                    Cl.SetKernelArg(kernel, 2, totalElements);
                    Cl.SetKernelArg(kernel, 3, ngi);
                    Cl.SetKernelArg(kernel, 4, (ngramChains.Length / longestChain));
                    Cl.SetKernelArg(kernel, 5, longestChain);
                    Cl.SetKernelArg(kernel, 6, length);
                    IntPtr[] workGroupSizePtr = new IntPtr[] { new IntPtr(globalList.Length) };
                    err = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out event0);
                    if (ErrorCode.Success != err)
                    {
                        Console.WriteLine("Error calling EnqueueNDRangeKernel: {0}", err.ToString());
                        throw new Exception(err.ToString());
                    }

                    Cl.Finish(cmdQueue);

                    byte[] results = new byte[globalList.Length];
                    Cl.EnqueueReadBuffer(cmdQueue, (IMem)memList, Bool.True, IntPtr.Zero, new IntPtr(globalList.Length * OpenCL.Net.TypeSize<byte>.SizeInt), results, 0, null, out event0);
                    var temp = Array.ConvertAll(results, b => Convert.ToChar(b));

                    returnData((temp, length));
                    
                    Cl.ReleaseMemObject(memList);
                    
                    //memList.Release();
                });



                //if (ErrorCode.Success != Cl.EnqueueWriteBuffer(cmdQueue, (IMem)memList, Bool.True, IntPtr.Zero, new IntPtr(OpenCL.Net.TypeSize<byte>.SizeInt * globalList.Length), Array.ConvertAll(globalList, c => Convert.ToByte(c)), 0, null, out event0))
                //    Console.WriteLine("Error calling EnqueueWriteBuffer");

                //memChains.Release();
                Cl.ReleaseMemObject(memChains);

                Cl.ReleaseKernel(kernel);
                Cl.ReleaseCommandQueue(cmdQueue);
                Cl.ReleaseContext(context);
                Cl.ReleaseProgram(program);
            });

            return t;
        }
    }
}
