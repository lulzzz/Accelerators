use OpenAiDatabase
go


/*
SQL script to build the suporting tables, stored procedures for Project Gutenberg Vectors IndexProject
*/
drop table if exists dbo.ProjectOpenAi;
CREATE TABLE [dbo].[ProjectOpenAi](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Source] [varchar](100) NOT NULL,
	[Document] [varchar](100) NOT NULL,
	[Url] [varchar](200) NOT NULL,
	[Paragraph] [varchar](6000) NOT NULL,
	[ParagraphEmbeddings] [varchar](max) NOT NULL,
 CONSTRAINT [pkProjectOpenAi] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/*
select * from ProjectOpenAi;
*/

drop table if exists dbo.ProjectOpenAiVectorsIndex;
CREATE TABLE [dbo].ProjectOpenAiVectorsIndex(
	[Id] [int] NOT NULL,
	[vector_value_id] [int] NOT NULL,
	[vector_value] [float] NULL
 CONSTRAINT pkProjectOpenAiVectorsIndex PRIMARY KEY CLUSTERED 
(
	[Id] ASC, [vector_value_id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
)
GO

/*
select * from dbo.ProjectOpenAiVectorsIndex;
*/

drop procedure if exists spSearchProjectOpenAiVectors;
GO
create procedure spSearchProjectOpenAiVectors
@jsonOpenAIEmbeddings nvarchar(max)
as
BEGIN

SET NOCOUNT ON;

drop table if exists #t;
select
    cast([key] as int) as [vector_value_id],
    cast([value] as float) as [vector_value]
into
	#t
from 
    openjson(@jsonOpenAIEmbeddings, '$') -- '$.data[0].embedding')

drop table if exists #results;
select top(10)
    v2.Id, 
    sum(v1.[vector_value] * v2.[vector_value]) / 
        (
            sqrt(sum(v1.[vector_value] * v1.[vector_value])) 
            * 
            sqrt(sum(v2.[vector_value] * v2.[vector_value]))
        ) as cosine_distance
into
    #results
from 
    #t v1
inner join 
    dbo.ProjectOpenAiVectorsIndex v2 on v1.vector_value_id = v2.vector_value_id
inner join
	dbo.ProjectOpenAi b1 on b1.Id = v2.Id
group by
    v2.Id
order by
    cosine_distance desc;

select 
    a.Id,
    a.Document,
	a.Source,
	a.Paragraph,
    --a.Url,
    r.cosine_distance as CosineDistance
from 
    #results r
inner join 
    dbo.ProjectOpenAi a on r.Id = a.Id
order by
    cosine_distance desc
END
GO

-- exec spCreateProjectOpenAiVectorsIndex;
drop procedure if exists spCreateProjectOpenAiVectorsIndex;
GO
create procedure spCreateProjectOpenAiVectorsIndex
as
BEGIN

set nocount on;

truncate table dbo.ProjectOpenAiVectorsIndex;

with cte as
(
    select 
        v.Id,
        cast(tv.[key] as int) as vector_value_id,
        cast(tv.[value] as float) as vector_value  
    from 
        [dbo].[ProjectOpenAi] as v
    cross apply 
        openjson(ParagraphEmbeddings) tv
)
insert into dbo.ProjectOpenAiVectorsIndex
(Id, vector_value_id, vector_value)
select
    Id,
    vector_value_id,
    vector_value
from
    cte;

END
GO
exec spCreateProjectOpenAiVectorsIndex;

/*
select * from dbo.ProjectOpenAiVectorsIndex
*/
