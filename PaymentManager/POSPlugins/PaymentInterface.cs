using PaymentManager.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tabsquare.Payment
{
    public interface PaymentInterface
    {
        string ExperimentSendHexa();

        Result DoTransaction(string merchant, string terminal, string orderId, decimal amount, string paymentType);
        
        Result DoSettlement(string merchant, string terminal, string orderId, decimal amount, string paymentType, string acquirer_bank);

        Result DoContinueSakuku(decimal amount, long refNo);

        Result DoInquirySakuku(decimal amount, long refNo);

        Result DoLogOn(string merchant, string terminal);

        Result DoTMS(string merchant, string terminal);

        Result CancelTransaction(string merchant, string terminal, string orderId, decimal amount, string paymentType);

        Result GetLastTransaction();

        Result GetTerminalStatus(string merchant, string terminal);

        Result GetLastApprovedTransaction(string merchant, string terminal, string orderId, decimal amount, string paymentType);

    }
}
