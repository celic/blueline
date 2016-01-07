# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  team_id  :integer
#  name     :string
#  position :integer
#  key      :string
#
# Indexes
#
#  index_players_on_key      (key) UNIQUE
#  index_players_on_team_id  (team_id)
#

FactoryGirl.define do
  factory :player do
    
  end

end
