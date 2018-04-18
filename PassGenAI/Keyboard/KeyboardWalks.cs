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

        public static bool CanOclWalk()
        {
            Platform[] platforms = Cl.GetPlatformIDs(out var err);
            Device[] devices = Cl.GetDeviceIDs(platforms[0], DeviceType.Gpu, out err);
            return devices.Any();
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
                        if (m.x + x >= 0 && m.y + y >= 0 && m.x + x <= keyboard[0].Length - 1 && m.y + y <= keyboard.Length - 1 /*&& !(m.x == 0 && m.y == 0)*/)
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
                return items.Distinct().ToArray();
            }).ToArray();

            var longestChain = tblNgram.Max(x => x.Count());


            tblNgram = tblNgram.Select(x =>
            {
                var n = new char[longestChain];
                Array.Copy(x, n, x.Length);
                return n;
            })
            .OrderByDescending(x => char.IsLetterOrDigit(x[0]))
            .ThenByDescending(x=>char.IsLetter(x[0]))
            .ThenBy(x=>x[0])
            .ToArray();

            var queue = new System.Collections.Concurrent.ConcurrentQueue<(char[] Data, int ItemLength)>();

            var t = ExecuteOnDevice(tblNgram, length, x => queue.Enqueue(x));

            while (t.Status != TaskStatus.Running) ; //Wait for it to get started

            ulong zeroCount = 0;
            ulong tooShort = 0;

            do
            {

                while (queue.TryDequeue(out var item))
                {
                    var walks = new Dictionary<string, int>(); //We will do unique per queue item but not globally unique
                    //var wlist = new List<string>();
                    for (int i = 0; i < item.Data.Length / item.ItemLength; i++)
                    {
                        if (item.Data[i * item.ItemLength] != '\0')
                        {
                            var walk = new string(item.Data, i * item.ItemLength, item.ItemLength).Replace("\0", "");
                            //wlist.Add(walk);
                            if (walk.Length == length)
                            {
                                if (walks.ContainsKey(walk)) walks[walk]++;
                                else
                                {
                                    walks.Add(walk, 1);
                                    yield return walk;
                                }
                            }
                            else
                                tooShort++;
                        }
                        else
                        {
                            //wlist.Add("");
                            zeroCount++;
                        }
                    }
                    walks = null;
                }
            } while (t.Status == TaskStatus.Running);
            //return walks.Keys.OrderBy(x => x);
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
                walk(__global char* tblNgram, __global char* globalList, long totalElements, int ngi, int chainCount, int longestChain, int length, int offset)
                {
                    int tid = get_global_id(0);
                    int writeIndex = tid*length;
	                int groupItemIndex = offset + get_global_id(0);
	                char previousChar;

                    globalList[tid*length] = tblNgram[ngi*longestChain];                    
                    previousChar = tblNgram[ngi*longestChain];                 

	                for (int pass = 1; previousChar != '\0' && pass < length; pass++)
	                {
		                int groupSize = (int)(pow((double)longestChain, (double)(length-pass-1)));
		                int groupCount = totalElements / groupSize;
		                int group = groupItemIndex / groupSize;
		                int ngl = 0;		                
			            for (int i = 0; i < chainCount; i++)
			            {
				            if (tblNgram[i*longestChain] == previousChar)
					            ngl = i;
			            }
		                int index = groupCount < longestChain ? group : (int)((((group * 1.0) / longestChain) - trunc((group * 1.0) / longestChain)) * longestChain);
		                globalList[writeIndex+pass] = tblNgram[ngl*longestChain + index];
                        previousChar = tblNgram[ngl*longestChain + index];
	                }

                    if(previousChar == '\0' /*|| tid % longestChain != 0*/)
                    {
                        for(int pass = 0; pass < length; pass++)
                        {
                            globalList[writeIndex+pass] = '\0';
                        }
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
                var longestChain = ngrams.Max(x => x.Count());

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
                    var totalElements = (long)Math.Pow(longestChain, length-1);
                    var maxSize = 500000;
                    if (ulong.TryParse(Cl.GetDeviceInfo(device, DeviceInfo.GlobalMemSize, out err).ToString(), out var globalMemSize))
                        maxSize = (int)(globalMemSize / 2);

                    for (int i = 0; (totalElements < maxSize && i == 0) || (i < (int)(totalElements / maxSize)); i++)
                    {
                        var runSize = (totalElements - (i * maxSize)) > maxSize ? maxSize : (totalElements - (i * maxSize));
                        var globalList = new char[runSize * length];
                        Mem memList = (Mem)Cl.CreateBuffer(context, MemFlags.WriteOnly, OpenCL.Net.TypeSize<byte>.SizeInt * globalList.Length, out err);

                        Cl.SetKernelArg(kernel, 0, memChains);
                        Cl.SetKernelArg(kernel, 1, memList);
                        Cl.SetKernelArg(kernel, 2, totalElements);
                        Cl.SetKernelArg(kernel, 3, ngi);
                        Cl.SetKernelArg(kernel, 4, (ngramChains.Length / longestChain));
                        Cl.SetKernelArg(kernel, 5, longestChain);
                        Cl.SetKernelArg(kernel, 6, length);
                        Cl.SetKernelArg(kernel, 7, i * maxSize);
                        IntPtr[] workGroupSizePtr = new IntPtr[] { new IntPtr(runSize) };
                        err = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out event0);
                        if (ErrorCode.Success != err)
                        {
                            Console.WriteLine("Error calling EnqueueNDRangeKernel: {0}", err.ToString());
                            throw new Exception(err.ToString());
                        }

                        Cl.Finish(cmdQueue);

                        byte[] results = new byte[globalList.Length];
                        Cl.EnqueueReadBuffer(cmdQueue, (IMem)memList, Bool.True, IntPtr.Zero, new IntPtr(globalList.Length * OpenCL.Net.TypeSize<byte>.SizeInt), results, 0, null, out event0);
                        //results = results.Where(x => x != 0).ToArray();
                        var temp = Array.ConvertAll(results, b => Convert.ToChar(b));

                        returnData((temp, length));

                        Cl.ReleaseMemObject(memList);
                    }
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
