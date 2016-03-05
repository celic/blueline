# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  name     :string
#  position :integer
#  key      :string
#  team_id  :integer
#
# Indexes
#
#  index_players_on_key  (key) UNIQUE
#

FactoryGirl.define do
  factory :player do
    
  end

end
