﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.Common.Core.OS;
using Microsoft.Common.Core.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using Microsoft.R.Host.Protocol;

namespace Microsoft.R.Host.UserProfile {
    public class RUserProfileServicesHelper {
        public static async Task CreateProfileAsync(int serverTimeOutms = 0, int clientTimeOutms = 0, IUserProfileServices userProfileService = null, CancellationToken ct = default(CancellationToken), ILogger logger = null) {
            userProfileService = userProfileService ?? new RUserProfileServices();
            await ProfileServiceOperationAsync(userProfileService.CreateUserProfile, NamedPipeServerStreamFactory.CreatorName, serverTimeOutms, clientTimeOutms, ct, logger);
        }

        public static async Task DeleteProfileAsync(int serverTimeOutms = 0, int clientTimeOutms = 0, IUserProfileServices userProfileService = null, CancellationToken ct = default(CancellationToken), ILogger logger = null) {
            userProfileService = userProfileService ?? new RUserProfileServices();
            await ProfileServiceOperationAsync(userProfileService.DeleteUserProfile, NamedPipeServerStreamFactory.DeletorName, serverTimeOutms, clientTimeOutms, ct, logger);
        }

        private static async Task ProfileServiceOperationAsync(Func<IUserCredentials, ILogger, IUserProfileServiceResult> operation, string name, int serverTimeOutms = 0, int clientTimeOutms = 0, CancellationToken ct = default(CancellationToken), ILogger logger = null) {
            using (NamedPipeServerStream server = NamedPipeServerStreamFactory.Create(name)) {
                await server.WaitForConnectionAsync(ct);

                ManualResetEventSlim forceDisconnect = new ManualResetEventSlim(false);
                try {
                    if (serverTimeOutms + clientTimeOutms > 0) {
                        Task.Run(() => {
                            // This handles Empty string input cases. This is usually the case were client is connected and writes a empty string.
                            // on the server side this blocks the ReadAsync indefinitely (even with the cancellation set). The code below protects 
                            // the server from being indefinitely blocked by a malicious client.
                            forceDisconnect.Wait(serverTimeOutms + clientTimeOutms);
                            if (server.IsConnected) {
                                server.Disconnect();
                                logger?.LogError(Resources.Error_ClientTimedOut);
                            }
                        }).DoNotWait();
                    }

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                        if (serverTimeOutms > 0) {
                            cts.CancelAfter(serverTimeOutms);
                        }

                        byte[] requestRaw = new byte[1024];
                        int bytesRead = 0;

                        while (bytesRead == 0 && !cts.IsCancellationRequested) {
                            bytesRead = await server.ReadAsync(requestRaw, 0, requestRaw.Length, cts.Token);
                        }

                        string json = Encoding.Unicode.GetString(requestRaw, 0, bytesRead);

                        var requestData = Json.DeserializeObject<RUserProfileServiceRequest>(json);

                        var result = operation?.Invoke(requestData, logger);

                        string jsonResp = JsonConvert.SerializeObject(result);
                        byte[] respData = Encoding.Unicode.GetBytes(jsonResp);

                        await server.WriteAsync(respData, 0, respData.Length, cts.Token);
                        await server.FlushAsync(cts.Token);
                    }

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                        if (clientTimeOutms > 0) {
                            cts.CancelAfter(clientTimeOutms);
                        }

                        // Waiting here to allow client to finish reading client should disconnect after reading.
                        byte[] requestRaw = new byte[1024];
                        int bytesRead = 0;
                        while (bytesRead == 0 && !cts.Token.IsCancellationRequested) {
                            bytesRead = await server.ReadAsync(requestRaw, 0, requestRaw.Length, cts.Token);
                        }

                        // if there was an attempt to write, disconnect.
                        server.Disconnect();
                    }
                } finally {
                    // server work is done.
                    forceDisconnect.Set();
                }
            }
        }
    }
}
