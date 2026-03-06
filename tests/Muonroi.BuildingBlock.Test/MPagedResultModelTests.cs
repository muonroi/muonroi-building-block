


namespace Muonroi.BuildingBlock.Test
{
    public class MPagedResultModelTests
    {
        private class DummyModel : MPagedResultModel { }

        [Fact]
        public void FirstRowOnPage_Computed_Correctly()
        {
            DummyModel model = new()
            {
                CurrentPage = 3,
                PageSize = 10
            };
            Assert.Equal(21, model.FirstRowOnPage);
        }

        [Fact]
        public void LastRowOnPage_Computed_Correctly()
        {
            DummyModel model = new()
            {
                CurrentPage = 2,
                PageSize = 10,
                RowCount = 15
            };
            Assert.Equal(15, model.LastRowOnPage);
        }

        [Fact]
        public void AdditionalData_Returns_Value_Or_Null()
        {
            DummyModel model = new();
            Assert.Null(model.AdditionalData);
            model.AdditionalData = "extra";
            Assert.Equal("extra", model.AdditionalData);
        }

        [Fact]
        public void CurrentPage_Returns_Correct_Value()
        {
            MPagedResult<string> result = new();
            Assert.Equal(0, result.CurrentPage);
            result.CurrentPage = 2;
            Assert.Equal(2, result.CurrentPage);
        }

        [Fact]
        public void PageSize_Returns_Correct_Value()
        {
            MPagedResult<string> result = new();
            Assert.Equal(0, result.PageSize);
            result.PageSize = 5;
            Assert.Equal(5, result.PageSize);
        }

        [Fact]
        public void RowCount_Returns_Correct_Value()
        {
            MPagedResult<string> result = new();
            Assert.Equal(0, result.RowCount);
            result.RowCount = 10;
            Assert.Equal(10, result.RowCount);
        }

        [Fact]
        public void PageCount_Returns_Calculated_Value()
        {
            MPagedResult<string> result = new();
            Assert.Equal(0, result.PageCount);

            result.RowCount = 5;
            result.PageSize = 2;
            Assert.Equal(3, result.PageCount);
        }
    }
}
