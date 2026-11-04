The idea here is to create a open source project with name RimBank.
We need to create in C# .net projet all the BIAN architecture starting from thier open spec following ddd, SOLID and advanced 
oop, FP, staying and way of working.
technical stuck would, c# .net latest version, db martin for events, postgresql, and any other tech we need on the road.
for the architecture we are going on hexagonal, ddd style where.
we should have /src and /tests, also we should respcet the namespace as following RimBank.Domain1.SubDomain and RimBank.Domain2.SubDomain
also for any infrsturture implementatione like for the api presentation layer we should have RimBank.Domain1.Api RimBank.Domain1.Postgre
for the project cs project we should have Core and other depends on it, and core should only define application and domain logic like following the next name space
RimBank.Domain1 the implementation can be in an other preoject fi in api RimBank.Domain1.Api or postgresql RimBank.Domain1.postgresql
no technical names only dd and flow and business capibilities.
the response type should be always problem details.


