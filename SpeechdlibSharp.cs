using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace SpeechdlibSharp;
public class SpeechDispatcher: IDisposable {
	private bool disposedValue;

	private Socket DispatcherSocket { get; }
	private NetworkStream DispatcherStream { get; }
	private static void CheckForSupportedOs(){
	if(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		throw new PlatformNotSupportedException($"Speech dispatcher is linux only, therefore it is not supported on your current operating system ({RuntimeInformation.OSDescription}");
	}
	public static SpeechDispatcher OpenConnectionWithDefaultAdress() {
	CheckForSupportedOs();
		UnixDomainSocketEndPoint Speechd = new(System.Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")+"/speech-dispatcher/speechd.sock");
	Socket SpeechdSoc = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
	SpeechdSoc.Connect(Speechd);
	return new(SpeechdSoc);
	}
	public static SpeechDispatcher OpenConnectionWithCustomAdress(string address) {
	CheckForSupportedOs();
	UnixDomainSocketEndPoint Speechd = new(address);
	Socket SpeechdSoc = new(AddressFamily.Unspecified, SocketType.Stream, ProtocolType.IP);
	SpeechdSoc.Connect(Speechd);
	return new(SpeechdSoc);
	}
	public uint Speak(string text, bool interrupt = true) {
	if(interrupt)
		SendCommand("CANCEL SELF\r\n");
	SendCommand("SPEAK\r\n");
	SendCommand(text, false);
	//we have two lines because first returns uint message id, second is queue result.
	var Result = SendCommand("\r\n.\r\n", ReplyLines: 2);
	//extract id
	return uint.Parse(Result.Reply[0].Split("-")[1]);
	}
	public void StopSpeech() {
	SendCommand("CANCEL SELF\r\n");
	}
	public void SetClientName(string ClientName) {
	SendCommand($"SET self CLIENT_NAME {System.Environment.UserName}:{ClientName}:default\r\n");
	}
	private SpeechDispatcher(Socket socket) {
	DispatcherSocket=socket;
	DispatcherStream=new(DispatcherSocket, true);
	}
	private CommandExecutionResult SendCommand(string Command, bool WaitForReply = true, int ReplyLines = 1) {
	byte[] CommandBytes = System.Text.UTF8Encoding.Default.GetBytes(Command);
	DispatcherSocket.Send(CommandBytes);
	CommandExecutionResult result = new(ReplyLines);
	if(WaitForReply) {
	for(int i = 0; i<ReplyLines; i++)
		result.Reply[i]=GetLine();
	}
	return result;
	}
	private string GetLine() {
	StringBuilder Result = new();
	while(true) {
	char c = (char)DispatcherStream.ReadByte();
	Result.Append(c);
	if(c==char.Parse("\n"))
		break;
	}
	return Result.ToString();
	}

	protected virtual void Dispose(bool disposing) {
	if(!disposedValue) {
	if(disposing) {
	SendCommand("QUIT\r\n");
	DispatcherStream.Close();
	DispatcherStream.Dispose();
	}

	disposedValue=true;
	}
	}
	public void Dispose() {
	Dispose(disposing: true);
	GC.SuppressFinalize(this);
	}
}
struct CommandExecutionResult(int ReplyLines) {
	public string[] Reply = new string[ReplyLines];
}