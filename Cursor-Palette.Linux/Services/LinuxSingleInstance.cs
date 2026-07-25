using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CursorPalette.Services;

namespace CursorPalette.Linux.Services;

public sealed class LinuxSingleInstance : ISingleInstance
{
	private const string SocketPath = "/tmp/cursor-palette-single-instance.sock";
	private const string NotifyMessage = "ACTIVATE";

	private static readonly byte[] NotifyBytes = Encoding.UTF8.GetBytes(NotifyMessage);

	public bool TryAcquire()
	{
		try
		{
			File.Delete(SocketPath);
		}
		catch
		{
		}

		try
		{
			var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			server.Bind(new UnixDomainSocketEndPoint(SocketPath));
			server.Listen(1);
			server.BeginAccept(OnClientConnected, server);

			return true;
		}
		catch
		{
			return false;
		}
	}

	public void NotifyExistingInstance()
	{
		try
		{
			using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			client.Connect(new UnixDomainSocketEndPoint(SocketPath));
			client.Send(NotifyBytes);
		}
		catch
		{
		}
	}

	public static event Action? ActivationRequested;

	private static void OnClientConnected(IAsyncResult result)
	{
		if (result.AsyncState is not Socket server)
			return;

		try
		{
			using var client = server.EndAccept(result);
			var buffer = new byte[256];
			client.Receive(buffer);

			if (Encoding.UTF8.GetString(buffer).TrimStart('\0').StartsWith(NotifyMessage))
				ActivationRequested?.Invoke();
		}
		catch
		{
		}
		finally
		{
			try
			{
				server.BeginAccept(OnClientConnected, server);
			}
			catch
			{
			}
		}
	}
}
