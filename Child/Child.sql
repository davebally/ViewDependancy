CREATE VIEW [dbo].[Child]
	AS SELECT * FROM [$(Parent)].[dbo].[Parent]