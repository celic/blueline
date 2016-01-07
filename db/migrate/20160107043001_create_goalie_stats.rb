class CreateGoalieStats < ActiveRecord::Migration
    def change
        create_table :goalie_stats do |t|
            t.references :player, index: true
            t.references :team, index: true
            t.references :opponent
            t.boolean :home
            t.integer :decision

            t.integer :record
            t.integer :goals_against
            t.integer :shots_against
            t.integer :saves
            t.decimal :save_percentage, precision: 5, scale: 2
            t.boolean :shutout

            t.integer :pim
            t.integer :toi
        end
    end
end
