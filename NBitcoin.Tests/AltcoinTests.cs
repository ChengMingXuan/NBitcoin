﻿using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NBitcoin.Tests
{
	[Trait("Altcoins", "Altcoins")]
	public class AltcoinTests
	{
		[Fact]
		public void NoCrashQuickTest()
		{
			HashSet<string> coins = new HashSet<string>();
			foreach(var network in NBitcoin.Altcoins.AltNetworkSets.GetAll().ToList())
			{
				Assert.True(coins.Add(network.CryptoCode.ToLowerInvariant()));
				Assert.NotEqual(network.Mainnet, network.Regtest);
				Assert.NotEqual(network.Regtest, network.Testnet);
				Assert.Equal(network.Regtest.NetworkSet, network.Testnet.NetworkSet);
				Assert.Equal(network.Mainnet.NetworkSet, network.Testnet.NetworkSet);
				Assert.Equal(network, network.Testnet.NetworkSet);
				Assert.Equal(NetworkType.Mainnet, network.Mainnet.NetworkType);
				Assert.Equal(NetworkType.Testnet, network.Testnet.NetworkType);
				Assert.Equal(NetworkType.Regtest, network.Regtest.NetworkType);
				Assert.Equal(network.CryptoCode, network.CryptoCode.ToUpperInvariant());
				Assert.Equal(network.Mainnet, Network.GetNetwork(network.CryptoCode.ToLowerInvariant() + "-mainnet"));
				Assert.Equal(network.Testnet, Network.GetNetwork(network.CryptoCode.ToLowerInvariant() + "-testnet"));
				Assert.Equal(network.Regtest, Network.GetNetwork(network.CryptoCode.ToLowerInvariant() + "-regtest"));
			}
		}


		[Fact]
		public void HasCorrectGenesisBlock()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var rpc = builder.CreateNode().CreateRPCClient();
				builder.StartAll();
				var actual = (rpc.GetBlock(0)).GetHash();
				Assert.Equal(builder.Network.GetGenesis().GetHash(), actual);
			}
		}

		[Fact]
		public void CanParseBlock()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var node = builder.CreateNode();
				builder.StartAll();
				var rpc = node.CreateRPCClient();
				rpc.Generate(100);
				var hash = rpc.GetBestBlockHash();
				Assert.NotNull(rpc.GetBlock(hash));
			}
		}

		[Fact]
		public void CanSignTransactions()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var node = builder.CreateNode();
				builder.StartAll();
				node.Generate(101);
				var rpc = node.CreateRPCClient();

				var alice = new Key().GetBitcoinSecret(builder.Network);
				var aliceAddress = alice.GetAddress();
				var txid = rpc.SendToAddress(aliceAddress, Money.Coins(1.0m));
				var tx = rpc.GetRawTransaction(txid);
				var coin = tx.Outputs.AsCoins().First(c => c.ScriptPubKey == aliceAddress.ScriptPubKey);

				TransactionBuilder txbuilder = new TransactionBuilder();
				txbuilder.SetConsensusFactory(builder.Network);
				txbuilder.AddCoins(coin);
				txbuilder.AddKeys(alice);
				txbuilder.Send(new Key().ScriptPubKey, Money.Coins(0.4m));
				txbuilder.SendFees(Money.Coins(0.00004m));
				txbuilder.SetChange(aliceAddress);
				var signed = txbuilder.BuildTransaction(true);
				Assert.True(txbuilder.Verify(signed));
				rpc.SendRawTransaction(signed);
			}
		}

		[Fact]
		public void CanParseAddress()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var node = builder.CreateNode();
				builder.StartAll();
				var addr = node.CreateRPCClient().SendCommand(RPC.RPCOperations.getnewaddress).Result.ToString();
				var addr2 = BitcoinAddress.Create(addr, builder.Network).ToString();
				Assert.Equal(addr, addr2);

				var address = new Key().PubKey.GetAddress(builder.Network);
				var isValid = ((JObject)node.CreateRPCClient().SendCommand("validateaddress", address.ToString()).Result)["isvalid"].Value<bool>();
				Assert.True(isValid);
			}
		}

		[Fact]
		public void CanSyncWithPoW()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var node = builder.CreateNode();
				builder.StartAll();
				node.Generate(100);

				var nodeClient = node.CreateNodeClient();
				nodeClient.VersionHandshake();
				ConcurrentChain chain = new ConcurrentChain(builder.Network);
				nodeClient.SynchronizeChain(chain, new Protocol.SynchronizeChainOptions() { SkipPoWCheck = false });
				Assert.Equal(100, chain.Height);
			}
		}

		[Fact]
		public void CanSyncWithoutPoW()
		{
			using(var builder = NodeBuilderEx.Create())
			{
				var node = builder.CreateNode();
				builder.StartAll();
				node.Generate(100);
				var nodeClient = node.CreateNodeClient();
				nodeClient.VersionHandshake();
				ConcurrentChain chain = new ConcurrentChain(builder.Network);
				nodeClient.SynchronizeChain(chain, new Protocol.SynchronizeChainOptions() { SkipPoWCheck = true });
				Assert.Equal(100, chain.Height);

				// If it fails, override Block.GetConsensusFactory()
				var b = node.CreateRPCClient().GetBlock(50);
				Assert.Equal(b.WithOptions(TransactionOptions.Witness).Header.GetType(), chain.GetBlock(50).Header.GetType());

				var b2 = nodeClient.GetBlocks().ToArray()[50];
				Assert.Equal(b2.Header.GetType(), chain.GetBlock(50).Header.GetType());
			}
		}
	}
}