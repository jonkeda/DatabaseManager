﻿SELECT * INTO CustomersBackup2017 FROM Customers;
SELECT CustomerName, ContactName INTO CustomersBackup2017 FROM Customers;
SELECT * INTO CustomersGermany FROM Customers WHERE Country = 'Germany';
SELECT Customers.CustomerName, Orders.OrderID INTO CustomersOrderBackup2017 FROM Customers LEFT JOIN Orders ON Customers.CustomerID = Orders.CustomerID;
