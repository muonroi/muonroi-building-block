namespace Muonroi.Mapper.Tests;

public class MappingConfigurationTests
{
    public class Source
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Extra { get; set; } = "extra";
    }

    public class Destination
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class WithReadOnly
    {
        public string Name { get; set; } = string.Empty;
        public string ReadOnlyProp => "computed";
    }

    public class TypeMismatch
    {
        public string Name { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty; // int in Source
    }

    private readonly SimpleMapper _mapper = new(new MappingConfiguration());

    [Fact]
    public void Map_Should_Copy_Matching_Properties()
    {
        Source src = new() { Name = "John", Age = 30, Extra = "ignored" };
        Destination dest = _mapper.Map<Destination>(src);

        dest.Name.Should().Be("John");
        dest.Age.Should().Be(30);
    }

    [Fact]
    public void Map_Should_Skip_ReadOnly_Destination_Properties()
    {
        Source src = new() { Name = "Test" };
        WithReadOnly dest = _mapper.Map<WithReadOnly>(src);

        dest.Name.Should().Be("Test");
        dest.ReadOnlyProp.Should().Be("computed");
    }

    [Fact]
    public void Map_Should_Skip_Type_Mismatched_Properties()
    {
        Source src = new() { Name = "Test", Age = 25 };
        TypeMismatch dest = _mapper.Map<TypeMismatch>(src);

        dest.Name.Should().Be("Test");
        dest.Age.Should().Be(string.Empty); // Not mapped due to type mismatch
    }

    [Fact]
    public void Map_Should_Be_Reusable_With_Same_Configuration()
    {
        Destination first = _mapper.Map<Destination>(new Source { Name = "First", Age = 1 });
        Destination second = _mapper.Map<Destination>(new Source { Name = "Second", Age = 2 });

        first.Name.Should().Be("First");
        second.Name.Should().Be("Second");
        second.Age.Should().Be(2);
    }
}

public class SimpleMapperTests
{
    public class PersonDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class PersonEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    private readonly SimpleMapper _mapper;

    public SimpleMapperTests()
    {
        _mapper = new SimpleMapper(new MappingConfiguration());
    }

    [Fact]
    public void Map_Generic_Should_Create_New_Instance_With_Matching_Properties()
    {
        PersonDto src = new() { Name = "Alice", Age = 25, Email = "alice@test.com" };

        PersonEntity result = _mapper.Map<PersonEntity>(src);

        result.Name.Should().Be("Alice");
        result.Age.Should().Be(25);
        result.Email.Should().Be("alice@test.com");
    }

    [Fact]
    public void Map_With_Existing_Destination_Should_Copy_Properties()
    {
        PersonDto src = new() { Name = "Bob", Age = 30 };
        PersonEntity dest = new() { Email = "existing@test.com" };

        PersonEntity result = _mapper.Map(src, dest);

        result.Should().BeSameAs(dest);
        result.Name.Should().Be("Bob");
        result.Age.Should().Be(30);
        result.Email.Should().Be(string.Empty); // Overwritten from source default
    }

    [Fact]
    public void Map_Object_Overload_Should_Copy_Properties()
    {
        PersonDto src = new() { Name = "Charlie", Age = 35 };
        PersonEntity dest = new();

        object result = _mapper.Map((object)src, (object)dest);

        result.Should().BeSameAs(dest);
        ((PersonEntity)result).Name.Should().Be("Charlie");
    }

    [Fact]
    public void Map_Generic_Should_Throw_On_Null_Source()
    {
        Action act = () => _mapper.Map<PersonEntity>(null!);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Map_With_Destination_Should_Throw_On_Null_Source()
    {
        Action act = () => _mapper.Map<PersonDto, PersonEntity>(null!, new PersonEntity());
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Map_With_Destination_Should_Throw_On_Null_Destination()
    {
        Action act = () => _mapper.Map(new PersonDto(), (PersonEntity)null!);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Map_Object_Should_Throw_On_Null_Source()
    {
        Action act = () => _mapper.Map(null!, new PersonEntity());
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Map_Object_Should_Throw_On_Null_Destination()
    {
        Action act = () => _mapper.Map(new PersonDto(), null!);
        act.Should().Throw<MArgumentException>();
    }
}
