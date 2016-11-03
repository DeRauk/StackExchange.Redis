using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace StackExchange.Redis.Tests
{
    using System.Threading.Tasks;

    [TestFixture]
    public class RealWorld
    {
        [Test]
        public void WhyDoesThisNotWork()
        {
            var sw = new StringWriter();
            Console.WriteLine("first:");
            using (var conn = ConnectionMultiplexer.Connect("localhost:6379,localhost:6380,name=Core (Q&A),tiebreaker=:RedisMaster,abortConnect=False", sw))
            {
                Console.WriteLine(sw);
                Console.WriteLine();
                Console.WriteLine("pausing...");
                Thread.Sleep(200);
                Console.WriteLine("second:");

                sw = new StringWriter();
                bool result = conn.Configure(sw);
                Console.WriteLine("Returned: {0}", result);
                Console.WriteLine(sw);
            }
        }

        /// <summary>
        /// Not really a unit test. I ran this in a console app to help prototype the timeout function but 
        /// didn't want to push a whole csproj to github, so I stuck it here.
        /// </summary>
        [Test]
        public void LatencyInjection()
        {
            var cm = ConnectionMultiplexer.Connect(string.Empty /* Put a connection string here */);
            cm.PreserveAsyncOrder = true;
            cm.DebugLatencyKeySubstring = "Test";
            cm.DebugLatency = 1000;

            var db = cm.GetDatabase();
            
            Task normalSetTask = DoTimedCacheSetWithTimeout(db, "NormalOperation", "SomeValue", 500);

            // This command will time out
            Task testSetTask = DoTimedCacheSetWithTimeout(db, "Test", "1", 500);

            // This command should complete normally, before testSetTask finishes
            Task<string> normalGetTask2 = DoTimedCacheGet(db, "NormalOperation");

            Task.WaitAll(normalSetTask, testSetTask, normalGetTask2);

            Console.ReadLine();
        }

        static async Task DoTimedCacheSetWithTimeout(IDatabase db, string key, string value, int timeoutInMS)
        {
            var sw = Stopwatch.StartNew();

            // Start the Redis operation
            Task t = db.StringSetAsync(key, value);

            // Start the timeout task
            var token = new CancellationTokenSource();
            Task timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(timeoutInMS), token.Token);

            // Wait for one of the tasks to finish
            await Task.WhenAny(timeoutTask, t);

            // Check to see if which task finished first
            if (!t.IsCompleted)
            {
                Console.WriteLine($"Set operation for {key} took {sw.ElapsedMilliseconds} milliseconds and was successful");
                token.Cancel();
            }
            else
            {
                Console.WriteLine($"Set operation for {key} took {sw.ElapsedMilliseconds} milliseconds and failed");
            }

            sw.Stop();
        }

        static async Task<string> DoTimedCacheGet(IDatabase db, string key)
        {
            var sw = Stopwatch.StartNew();
            var value = await db.StringGetAsync(key);
            Console.WriteLine($"Get operation for {key} took {sw.ElapsedMilliseconds} milliseconds and retrieved '{value}'");
            sw.Stop();

            return value;
        }
    }
}
