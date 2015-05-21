CREATE PROCEDURE [ChildSchema].[Procedure1]
	@param1 int = 0,
	@param2 int
AS
	SELECT * from [$(Parent)].[dbo].[Parent]
RETURN 0
