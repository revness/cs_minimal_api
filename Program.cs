namespace TodoApi;
using System;

using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

public class TodoDTO
{
    public int Id { get; set; }
    [Required]
    [StringLength(100, ErrorMessage = "Title is too long.")]
    public required string Title { get; set; }
    public required string Content { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DueDate { get; set; }
    public int CategoryId { get; set; }
    public  CategoryDTO Category { get; set; }
}

public class CategoryDTO
{
    public int Id { get; set; }
    public string Name { get; set; }

    public List<TodoDTO> Todos { get; set; }
}
public class Todo
{

    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }


    public bool IsDeleted { get; set;}
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get;  set; }

    public DateTime DueDate {get; set;}
    //creates a separate category table
    public Category Category {get; set;}
    public int CategoryId {get; set;}
}

public class Category
{
    public int Id {get; set;}
    public string Name {get; set;}
    public List<Todo> Todos {get; set;} = new List<Todo>();
}

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options) : base(options) { }
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Todo>()
        .HasOne(t => t.Category)
        .WithMany(c => c.Todos)
        .HasForeignKey(t => t.CategoryId);

    modelBuilder.Entity<Category>()
        .HasKey(c => c.Id);

    modelBuilder.Entity<Category>()
        .Property(c => c.Id)
        .ValueGeneratedOnAdd();
}
}

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add database context
        builder.Services.AddDbContext<TodoDb>(opt => 
            opt.UseInMemoryDatabase("TodoDb"));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Add services to the container
        builder.Services.AddEndpointsApiExplorer();

        // Configure Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo 
            { 
                Title = "TodoAPI", 
                Description = "Keep track of your tasks", 
                Version = "v1"
            });
        });

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => 
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
            });
        }

        // Use CORS before routing
        app.UseCors("AllowAll");

        // API Endpoints

        // Category Endpoints
   app.MapGet("/categories", async (TodoDb db) =>
{
    var categories = await db.Categories.Include(c => c.Todos).ToListAsync();
    var categoryDTOs = categories.Select(c => new CategoryDTO
    {
        Id = c.Id,
        Name = c.Name,
        Todos = c.Todos.Select(t => new TodoDTO
        {
            Id = t.Id,
            Title = t.Title,
            Content = t.Content,
            IsDeleted = t.IsDeleted,
            IsCompleted = t.IsCompleted,
            CreatedAt = t.CreatedAt,
            DueDate = t.DueDate,
            CategoryId = t.CategoryId
        }).ToList()
    }).ToList();

    return categoryDTOs;
});

        app.MapGet("/categories/{id}", async (int id, TodoDb db) =>
            await db.Categories.Include(c => c.Todos)
                .FirstOrDefaultAsync(c => c.Id == id)
                is Category category
                    ? Results.Ok(category)
                    : Results.NotFound());

        app.MapPost("/categories", async (Category category, TodoDb db) =>
        {
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/categories/{category.Id}", category);
        });

        app.MapPut("/categories/{id}", async (int id, Category inputCategory, TodoDb db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = inputCategory.Name;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapDelete("/categories/{id}", async (int id, TodoDb db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return Results.Ok(category);
        });

        // Todo Endpoints
        // GET all todos
        app.MapGet("/todos", async (TodoDb db) =>
{
    var todos = await db.Todos
        .Include(t => t.Category)
        .Where(t => !t.IsDeleted)
        .ToListAsync();

    var todoDTOs = todos.Select(t => new TodoDTO
    {
        Id = t.Id,
        Title = t.Title,
        Content = t.Content,
        IsDeleted = t.IsDeleted,
        IsCompleted = t.IsCompleted,
        CreatedAt = t.CreatedAt,
        DueDate = t.DueDate,
        CategoryId = t.CategoryId,
        Category = new CategoryDTO
        {
            Id = t.Category.Id,
            Name = t.Category.Name
        }
    }).ToList();

    return todoDTOs;
});

        // GET todo by id
app.MapGet("/todos/{id}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos
        .Include(t => t.Category)
        .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

    if (todo == null)
        return Results.NotFound();

    var todoDTO = new TodoDTO
    {
        Id = todo.Id,
        Title = todo.Title,
        Content = todo.Content,
        IsDeleted = todo.IsDeleted,
        IsCompleted = todo.IsCompleted,
        CreatedAt = todo.CreatedAt,
        DueDate = todo.DueDate,
        CategoryId = todo.CategoryId,
        Category = new CategoryDTO
        {
            Id = todo.Category.Id,
            Name = todo.Category.Name
        }
    };

    return Results.Ok(todoDTO);
});

        // POST new todo
        app.MapPost("/todos", async (Todo todo, TodoDb db) =>
        {
            if (!await db.Categories.AnyAsync(c => c.Id == todo.CategoryId))
                return Results.BadRequest("Category not found");
            todo.CreatedAt = DateTime.UtcNow;
            todo.IsCompleted = false;
            todo.IsDeleted = false;
            db.Todos.Add(todo);
            await db.SaveChangesAsync();

            return Results.Created($"/todos/{todo.Id}", todo);
        });

        // PUT update todo
        app.MapPatch("/todos/{id}", async (int id, Todo inputTodo, TodoDb db) =>
        {
            var todo = await db.Todos.FindAsync(id);
    

            if (todo is null || todo.IsDeleted) return Results.NotFound();

            if (inputTodo.CategoryId != 0 && !await db.Categories.AnyAsync(c => c.Id == inputTodo.CategoryId))
                return Results.BadRequest("Category not found");

            todo.Title = inputTodo.Title;
            todo.Content = inputTodo.Content;
            todo.IsCompleted = inputTodo.IsCompleted;
            todo.DueDate = inputTodo.DueDate;
            if (inputTodo.CategoryId != 0) todo.CategoryId = inputTodo.CategoryId;

            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        // DELETE todo
        app.MapDelete("/todos/{id}", async (int id, TodoDb db) =>
        {
          var todo = await db.Todos.FindAsync(id);
            if (todo is null) return Results.NotFound();

            todo.IsDeleted = true; // Soft delete
            await db.SaveChangesAsync();
            return Results.Ok(todo);
        });

        app.Run();
    }
}