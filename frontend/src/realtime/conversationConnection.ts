import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr";

export function createConversationConnection(baseUrl: string, accessToken: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseUrl.replace(/\/$/, "")}/hubs/conversations`, {
      accessTokenFactory: () => accessToken,
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
    })
    .withAutomaticReconnect([0, 1000, 3000, 5000])
    .configureLogging(LogLevel.Warning)
    .build();
}
