namespace Eventuous.Kafka;

using System;
using System.Text;
using Confluent.Kafka;
using MessageHeaders = Dictionary<string, string>;

public static class HeadersExtensions
{
    public static Headers Encode(this Dictionary<string,string> messageHeaders, Func<string?, byte[]> getBytes) {
        Ensure.NotNull(messageHeaders, nameof(messageHeaders));
        Ensure.NotNull(getBytes, nameof(getBytes));

        var headers = new Headers();

        foreach (var (key, value) in messageHeaders)
            try {
                headers.Add(key, value is null ? Array.Empty<byte>() : getBytes(value));
            }
            catch (Exception ex) {
                throw new Exception($"Failed to encode header [{key}]", ex);
            }

        return headers;
    }

    public static Headers Encode(this MessageHeaders messageHeaders) => Encode(messageHeaders, value => Encoding.UTF8.GetBytes(value!));

    public static MessageHeaders Decode(this Headers? headers) {
        if (headers is null || headers.Count == 0) return new MessageHeaders();

        var decoded = new MessageHeaders();

        foreach (var header in headers) {
            if (header is null) continue;

            try {
                decoded.Add(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
            }
            catch (Exception ex) {
                throw new Exception($"Failed to decode header {header.Key}", ex);
            }
        }

        return decoded;
    }

    public static bool TryDecodeValue(this Headers headers, string key, Func<byte[], string?> getString, out string? value) {
        Ensure.NotNull(headers, nameof(headers));
        Ensure.NotEmptyString(key, nameof(key));

        if (headers.TryGetLastBytes(key, out var bytes)) {
            value = bytes.Length > 0 ? getString(bytes) : null;
            return true;
        }

        value = null;
        return false;
    }

    public static bool TryDecodeValue(this Headers headers, string key, out string? value)
        => TryDecodeValue(headers, key, Encoding.UTF8.GetString, out value);

    public static string? DecodeValue(this Headers headers, string key, Func<byte[], string?> getString) {
        Ensure.NotNull(headers, nameof(headers));
        Ensure.NotEmptyString(key, nameof(key));

        TryDecodeValue(headers, key, getString, out var value);

        return value;
    }

    public static string? DecodeValue(this Headers headers, string key)
        => DecodeValue(headers, key, Encoding.UTF8.GetString);
}