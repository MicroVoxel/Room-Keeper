using System;

[Serializable]
public enum IAPProductKey
{
    vip1,
    vip2, 
    vip3, 
    vip4,
}

[Serializable]
public class IAPPPayData
{
    public string Payload;
    public string Store;
    public string TransactionID;
}

[Serializable]
public class IAPPPayload
{
    public string json;
    public string signature;
    public IAPPPayloadData payloadData;
}

[Serializable]
public class IAPPPayloadData
{
    public string orderId;
    public string packageName;
    public string productId;
    public long purchaseTime;
    public int purchaseState;
    public string purchaseToken;
    public int quantity;
    public bool acknowledged;
}