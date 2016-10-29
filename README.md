# Nested.Automapper 

Nested.Automapper is a library that converts a flat list of Dictiornary<string,object> objects or a list of loosely typed dynamic objects which can be converted into IDictionary<stirng,object> into a list of nested strongly typed objects.


## When to use Nested.Automapper

There are a number of microORM's available which do not allow mapping queries to strongly typed nested classes, either not supporting strongly typed data all together in favour of dynamic or only supporting simple non-nested strongly typed classes; eg:- Dapper. This library is meant to be used on top of existing micro-ORM's to provide this functionality.


## Advantages of Nested.Automapper

When compared to other Automapper, Nested.Automapper is designed to be **extremely light and performant** . It internally uses IL.Emit to dynamically generate custom functions for mapping each class which once generated have the same performance as custom hand written code.


## How to use Nested.Automapper

### Simple example using Dapper

Lets say your are trying to access the following table on your DB

Person
| Name | Age | Occupation |
|---|---|---|
| John | 23 | FireFighter |
| Jane | 32 | PoliceOfficer |
| Alive | 23 | Website Designer |


Your code  for accessing the Db and then using Nested.Automapper to Map the data to strongly typed would look something like this.
```
public class Person 
{
	public string Name {get; set;}
	public int Age {get; set;}
	public string Occupation {get; set;}
}

public void static SimpleMappingExample() 
{
 var dynamicData =  dbConnection.Query(@"
	 select Name,Age,Occupation from Person Where Name = @Name
	", new { Name = "Jane" });
 var stronglyTypeData = Nested.Automapper.Mapper.Map<Person>(dynamicData);
}
```

###Nested example using Dapper

Lets say you are trying to access the following related tables using a single query

Person
| PersonId | Name | Age | Occupation |
|---|---|---|---|
| 1 | John | 23 | FireFighter |
| 2 | Jane | 32 | PoliceOfficer |
| 3 | Alive | 23 | Website Designer |

Vehicles
| VechicleId | Make | OwnerId |
|---|---|---|
| 1 | Toyota Prius | 1 |
| 2 | Honda Civic | 2 |
| 3 | Audi A4 | 2 |


```
public class Person 
{
	[Key]
	public int PersonId {get; set;}
	public string Name {get; set;}
	public int Age {get; set;}
	public string Occupation {get; set;}
	public List<Vehicles> Vehicles {get; set;}
}

public class Vehicle
{
	[Key]
	public int VehicleId {get; set;}
	public string Make {get; set;}
}


public void static NestedMappingExample() 
{
	var dynamicData =  dbConnection.Query(@"
		 select 
		 p.Name
		 ,p.Age
		 ,p.Occupation 
		 ,v.VehicleId as [Vehicles.VehicleId]
		 ,v.Make as [Vehicles.Make]
		 from Person p join Vehicle v on p.PersonId = v.OwnerId
		");
	 var stronglyTypeData = Nested.Automapper.Mapper.Map<Person>(dynamicData);
	
	foreach(var person in stronglyTypeData )
	{
		Console.WriteLine(string.Format("{0} owns {1} vehicles", person.Name, person.Vehicles.Count));
	}
 
}
```

####Output
> John owns 1 vehicles
> Jane owns 2 vehicles
> Alice owns 0 vehicles

####Points to Note

 1.  [Key] attribute used to mark primary key columns, you can find this in System.ComponentModel.DataAnnotations.dll and should be applied to all the primary key columns
 2. An alias has been provided for each of the nested columns which indicates the property within which data must be populated. eg:- [Vehicles.VehicleId] lets Nested.Automapper know that the VehicleId property within the Vehicles Propery of Person should be populated from this column


##Performance Benchmarks

> Soon ™ 
 
