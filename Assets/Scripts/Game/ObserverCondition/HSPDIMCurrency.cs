using Framework.HSPDIMAlgo;
using HSPDIMAlgo;

public class HSPDIMCurrency : HSPDIMEntity
{
    Currency currency;
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (IsServerInitialized)
        {
            currency = GetComponent<Currency>();
        }
    }
}
