class ChangeGoalieStatRecordColumnName < ActiveRecord::Migration
    def change
        rename_column :goalie_stats, :record, :verdict
    end
end
