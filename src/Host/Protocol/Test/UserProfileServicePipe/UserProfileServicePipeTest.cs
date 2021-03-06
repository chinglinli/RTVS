﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Common.Core;
using Microsoft.Common.Core.Json;
using Microsoft.Common.Core.OS;
using Microsoft.R.Host.UserProfile;
using Microsoft.UnitTests.Core.FluentAssertions;
using Microsoft.UnitTests.Core.Threading;
using Microsoft.UnitTests.Core.XUnit;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.R.Host.Protocol.Test.UserProfileServicePipe {
    public class UserProfileServicePipeTest {

        private async Task<UserProfileResultMock> CreateProfileClientTestWorkerAsync(string input, CancellationToken ct = default(CancellationToken)) {
            string jsonResp = null;
            using (NamedPipeClientStream client = new NamedPipeClientStream("Microsoft.R.Host.UserProfile.Creator{b101cc2d-156e-472e-8d98-b9d999a93c7a}")) {
                await client.ConnectAsync(ct);
                byte[] data = Encoding.Unicode.GetBytes(input);

                await client.WriteAsync(data, 0, data.Length, ct);
                await client.FlushAsync(ct);

                byte[] responseRaw = new byte[1024];
                var bytesRead = await client.ReadAsync(responseRaw, 0, responseRaw.Length, ct);
                jsonResp = Encoding.Unicode.GetString(responseRaw, 0, bytesRead);
            }
            return Json.DeserializeObject<UserProfileResultMock>(jsonResp);
        }

        private async Task CreateProfileTestRunnerAsync(IUserProfileServices creator, string input, bool isValidParse, bool isValidAccount, bool isExistingAccount, int serverTimeOut, int clientTimeOut) {
            ManualResetEventSlim testDone = new ManualResetEventSlim(false);
            Task.Run(async () => {
                try {
                    if (isValidParse) {
                        Func<Task> f = async () => await RUserProfileServicesHelper.CreateProfileAsync(serverTimeOutms: serverTimeOut, clientTimeOutms: clientTimeOut, userProfileService: creator);
                        f.ShouldNotThrow();
                    } else {
                        Func<Task> f = () => RUserProfileServicesHelper.CreateProfileAsync(serverTimeOutms: serverTimeOut, clientTimeOutms: clientTimeOut, userProfileService: creator);
                        await f.ShouldThrowAsync<Exception>();
                    }
                } finally {
                    testDone.Set();
                }
            }).DoNotWait();

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(clientTimeOut))) {
                UserProfileResultMock result = await CreateProfileClientTestWorkerAsync(input, cts.Token);
                if (isValidParse) {
                    result.Error.Should().Be((uint)(isValidAccount ? 0 : 13));
                    result.ProfileExists.Should().Be(isExistingAccount);
                } else {
                    result.Should().BeNull();
                }
            }

            testDone.Wait(serverTimeOut + clientTimeOut);
        }

        private async Task CreateProfileFuzzTestRunnerAsync(IUserProfileServices creator, string input, int serverTimeOut, int clientTimeOut) {
            var task = Task.Run(async () => {
                try {
                    await RUserProfileServicesHelper.CreateProfileAsync(serverTimeOutms: serverTimeOut, clientTimeOutms: clientTimeOut, userProfileService: creator);
                } catch (JsonReaderException) {
                    // expecting JSON parsing to fail
                    // JSON parsing may fail due to randomly generated strings as input.
                }
            });

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(clientTimeOut))) {
                UserProfileResultMock result = await CreateProfileClientTestWorkerAsync(input, cts.Token);
                // fuzz test parsing succeeded, the creator always fails for this test.
                result?.Error.Should().Be(13);
            }

            await ParallelTools.When(task, serverTimeOut + clientTimeOut);
        }

        [CompositeTest]
        // valid test data
        [InlineData("{\"Username\":\"testname\", \"Domain\":\"testdomain\", \"Password\":\"testPassword\"}", "testname", "testdomain", "testPassword", true, true, false)]
        // Missing quote
        [InlineData("{Username\":\"testname\", \"Domain\":\"testdomain\", \"Password\":\"testPassword\"}", null, null, null, false, false, false)]
        // Missing closing parenthesis
        [InlineData("{", null, null, null, false, false, false)]
        // empty json object
        [InlineData("{}", null, null, null, false, false, false)]
        // No username, domain and password
        [InlineData("{\"Username\":, \"Domain\":, \"Password\":}", null, null, null, false, false, false)]
        // empty username domain password
        [InlineData("{\"Username\": \"\", \"Domain\": \"\", \"Password\": \"\"}", "", "", "", true, false, false)]
        // whitespace input string
        [InlineData("                     ", null, null, null, true, false, false)]
        // empty string
        [InlineData("", null, null, null, false, false, false)]
        public async Task CreateProfileTest(string input, string username, string domain, string password, bool isValidParse, bool isValidAccount, bool isExistingAccount) {
            var creator = UserProfileServiceMock.Create(username, domain, password, isValidParse, isValidAccount, isExistingAccount);
            await CreateProfileTestRunnerAsync(creator, input, isValidParse, isValidAccount, isExistingAccount, 500, 500);
        }

        [Test]
        [Category.FuzzTest]
        public async Task CreateProfileFuzzTest() {
            string inner = "\"Username\": {0}, \"Domain\": {1}, \"Password\":{2}";
            for (int i = 0; i < 100000; ++i) {

                byte[] usernameBytes = GenerateBytes();
                byte[] domainBytes = GenerateBytes();
                byte[] passwordBytes = GenerateBytes();

                string username = Encoding.Unicode.GetString(usernameBytes);
                string domain = Encoding.Unicode.GetString(domainBytes);
                string password = Encoding.Unicode.GetString(passwordBytes);

                string json = "{" + string.Format(inner, username, domain, password) + "}";
                
                string testResult = string.Empty;
                UserProfileServiceFuzzTestMock creator = new UserProfileServiceFuzzTestMock();

                try {
                    await CreateProfileFuzzTestRunnerAsync(creator, json, 100, 100);
                } catch (IOException) {
                    // expect pipe to fail. The client side pipe throws an IOException when the server side pipe 
                    // closes due to IO error or attempt to access unauthorized memory.
                } catch (TaskCanceledException) {
                } catch (OperationCanceledException) {
                }
            }
        }

        private byte[] GenerateBytes() {
            Random rd = new Random();
            byte[] data = new byte[rd.Next(0, 1024)];
            rd.NextBytes(data);
            return data;
        }
    }
}
