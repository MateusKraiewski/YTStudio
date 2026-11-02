namespace YTStudio.Core.Vault;

/// <summary>Erro com mensagem pronta para mostrar ao usuário.</summary>
public sealed class VaultException(string message, Exception? inner = null) : Exception(message, inner);
